#!/usr/bin/env bash
# Build libgnubgapi.so on Linux.
#
# Reads gnubg sources from the pinned submodule at native/gnubg/.
# Does NOT edit anything inside the submodule — it just compiles against it.
# Output goes to ../runtimes/linux-x64/native/libgnubgapi.so by default.
set -euo pipefail

NATIVE_DIR="$(cd "$(dirname "$0")" && pwd)"
GNUBG_DIR="$NATIVE_DIR/gnubg"

# The wrapper emits gnubg's REAL contact inputs by calling gnubg's own
# CalculateHalfInputs + menOffAll (eval.c). Those are `static`, so we expose them
# with a visibility-ONLY patch (static -> extern; no logic change) applied to the
# pristine submodule at build time. The patch lives in THIS repo, never committed
# into the gnubg clone — so native/gnubg stays an unmodified upstream checkout.
# Reverted on exit so the submodule working tree is left clean.
# Tools this needs beyond a compiler, checked before anything else runs.
#
# Both `patch` probes below redirect stderr to /dev/null, so a MISSING patch
# command looked exactly like a patch that would not apply — and the script then
# blamed gnubg's source: "cannot apply ... (gnubg eval.c drift?)". That message
# sent whoever read it to diff eval.c against upstream, which is nowhere near
# the problem. The Windows job failed that way for two months. A tool that is
# not installed should say so.
for tool in patch git; do
    command -v "$tool" >/dev/null 2>&1 || {
        echo "ERROR: '$tool' is not installed, and this build needs it." >&2
        exit 1
    }
done

EXPOSE_PATCH="$NATIVE_DIR/gnubg-expose-inputs.patch"
PATCH_APPLIED=0
cleanup() {
    # Restore the exact upstream blob (also normalises any EOL/stat churn `patch`
    # introduces), leaving the pristine submodule clean.
    if [ "$PATCH_APPLIED" = "1" ]; then
        git -C "$GNUBG_DIR" checkout -- eval.c 2>/dev/null || true
    fi
}
trap cleanup EXIT

# Target detection: GNUBGAPI_TARGET overrides; otherwise infer from `uname`.
# Windows builds run under MSYS2/MinGW64 and uname reports MINGW64_NT-*.
TARGET="${GNUBGAPI_TARGET:-}"
if [ -z "$TARGET" ]; then
    case "$(uname -s)" in
        MINGW64_NT*|MSYS_NT*|CYGWIN_NT*) TARGET="win-x64" ;;
        Linux)
            case "$(uname -m)" in
                x86_64)        TARGET="linux-x64" ;;
                aarch64|arm64) TARGET="linux-arm64" ;;
                *) echo "ERROR: unrecognised Linux machine $(uname -m); set GNUBGAPI_TARGET" >&2; exit 1 ;;
            esac
            ;;
        # macOS reports arm64 for Apple Silicon, where Linux reports aarch64 --
        # and the .NET RID is osx-*, not macos-*. Three vocabularies for two
        # machines; each is right in its own world and none should be corrected
        # to match another.
        Darwin)
            case "$(uname -m)" in
                x86_64) TARGET="osx-x64" ;;
                arm64)  TARGET="osx-arm64" ;;
                *) echo "ERROR: unrecognised macOS machine $(uname -m); set GNUBGAPI_TARGET" >&2; exit 1 ;;
            esac
            ;;
        *) echo "ERROR: unrecognised host $(uname -s); set GNUBGAPI_TARGET" >&2; exit 1 ;;
    esac
fi

# TARGET_OS, not TARGET, is what the compiler flags below branch on. Every
# difference between these builds is object-format — ELF versus PE versus
# Mach-O: position independence, import libraries, symbol export, where the
# linker looks for libraries — and none of it is about the instruction set.
# Branching on the full RID made the two look like x86 decisions and was the
# only thing standing between this script and an arm64 build.
case "$TARGET" in
    linux-x64|linux-arm64) OUT_NAME="libgnubgapi.so";    TARGET_OS="linux" ;;
    win-x64)               OUT_NAME="libgnubgapi.dll";   TARGET_OS="windows" ;;
    osx-x64|osx-arm64)     OUT_NAME="libgnubgapi.dylib"; TARGET_OS="macos" ;;
    *) echo "ERROR: unsupported target '$TARGET'" >&2; exit 1 ;;
esac

# macOS has no system package manager, so the dependencies this build needs --
# glib, gmp, and the autotools -- come from Homebrew, and Homebrew's prefix is
# NOT on the compiler's default search path (/opt/homebrew on Apple Silicon,
# /usr/local on Intel). Resolve it once here rather than hard-coding either.
#
# bison and flex matter more than they look: macOS ships bison 2.3 from 2006 at
# /usr/bin, gnubg's autoreconf wants something this century, and Homebrew's are
# keg-only so they are installed but deliberately not on PATH. Putting them
# ahead of Apple's is the difference between `autoreconf -fi` working and a
# grammar error nobody would connect to the OS version.
if [ "$TARGET_OS" = "macos" ]; then
    command -v brew >/dev/null 2>&1 || {
        echo "ERROR: Homebrew is required to build on macOS (glib, gmp, autotools)." >&2
        echo "Install it from https://brew.sh, then: brew install glib gmp pkg-config autoconf automake libtool gettext bison flex" >&2
        exit 1
    }
    BREW_PREFIX="$(brew --prefix)"
    for keg in bison flex gettext; do
        keg_bin="$BREW_PREFIX/opt/$keg/bin"
        [ -d "$keg_bin" ] && PATH="$keg_bin:$PATH"
    done
    export PATH
    # gmp ships no pkg-config file, so unlike glib it cannot be discovered --
    # its include and lib directories have to be named.
    GMP_PREFIX="$BREW_PREFIX/opt/gmp"
    [ -d "$GMP_PREFIX" ] || { echo "ERROR: gmp is not installed. Run: brew install gmp" >&2; exit 1; }
fi
OUT_DIR="${1:-$NATIVE_DIR/../runtimes/$TARGET/native}"

if [ ! -f "$GNUBG_DIR/configure.ac" ]; then
    echo "ERROR: gnubg submodule not initialised at $GNUBG_DIR" >&2
    echo "Run: git submodule update --init --recursive" >&2
    exit 1
fi

mkdir -p "$OUT_DIR"

# Step 1: configure the gnubg tree so config.h and per-platform feature
# macros exist for the source files we're about to compile. We disable
# everything we don't need — Python bindings, GTK UI, 3D board.
echo "==> Configuring gnubg ($(cd "$GNUBG_DIR" && git describe --always))"
cd "$GNUBG_DIR"
autoreconf -fi
# Linux requires -fPIC throughout for the resulting .a's to be linkable into
# our shared .so. Windows DLLs don't need it (and emit a warning if asked).
# macOS does not need it either, for a different reason: clang compiles PIC by
# default there, so the .a's are already linkable into a dylib.
# Inject CFLAGS at configure time so gnubg's Makefiles bake it in everywhere
# instead of trying to override per-target later.
CFG_CFLAGS="-O3 -ffast-math"
if [ "$TARGET_OS" = "linux" ]; then CFG_CFLAGS="$CFG_CFLAGS -fPIC"; fi
# --without-libcurl is not optional and is not about size. gnubg's dice.c calls
# getDiceRandomDotOrg() -- which fetches dice from random.org over the network --
# whenever configure finds libcurl. That function lives in randomorg.c, which is
# deliberately NOT in the source list below, so the reference does not resolve
# and the link fails.
#
# Linux and Windows never hit this only because their build images have no curl
# development package, so the check quietly failed and the code was compiled out.
# macOS ships libcurl, the check succeeded, and the arm64 link died on an
# undefined _getDiceRandomDotOrg. Relying on a dependency happening to be absent
# is not a decision; this makes it one.
#
# It is also the behaviour we want on every platform. This is a headless analysis
# daemon: it must never reach the network to roll dice.
CFLAGS="$CFG_CFLAGS" ./configure --without-python --without-gtk --without-board3d --without-libcurl --quiet

# Step 2: build gnubg's lib/libevent.la — the only internal static lib our
# wrapper transitively depends on (queues + event loop helpers used by
# multithread.c). We do NOT run `make` at the gnubg root; that would build
# the full app, which we don't need and which has dependencies (gtk, png,
# freetype, ...) that aren't relevant for an eval-only library.
echo "==> Building gnubg lib/"
# nproc is GNU coreutils and does not exist on macOS; sysctl is the BSD answer.
# Asked in that order so Linux and MSYS2 keep the tool they already have.
if command -v nproc >/dev/null 2>&1; then
    JOBS="$(nproc)"
elif command -v sysctl >/dev/null 2>&1; then
    JOBS="$(sysctl -n hw.ncpu)"
else
    JOBS=1
fi
make -C lib -j"$JOBS"

# Step 3: compile our wrapper, pulling required gnubg source files out of
# the submodule by absolute path. The list mirrors what api/Makefile.am
# upstream had as libgnubgapi_la_SOURCES.
# Expose gnubg's static input fns for the wrapper (visibility only; see top of file).
# GNU patch (not `git apply`) — tolerant of LF/CRLF between git builds; --batch keeps
# it non-interactive (never prompts). --forward makes an already-applied patch a no-op.
if [ -f "$EXPOSE_PATCH" ]; then
    if patch -p1 -d "$GNUBG_DIR" --forward --batch --dry-run < "$EXPOSE_PATCH" >/dev/null 2>&1; then
        echo "==> Applying visibility patch (expose CalculateHalfInputs/menOffAll; logic untouched)"
        patch -p1 -d "$GNUBG_DIR" --forward --batch < "$EXPOSE_PATCH" >/dev/null
        PATCH_APPLIED=1
    elif patch -R -p1 -d "$GNUBG_DIR" --batch --dry-run < "$EXPOSE_PATCH" >/dev/null 2>&1; then
        echo "==> Visibility patch already applied"
    else
        echo "ERROR: cannot apply $EXPOSE_PATCH (gnubg eval.c drift?)" >&2
        exit 1
    fi
fi

echo "==> Compiling libgnubgapi.so"
GNUBG_SOURCES=(
    eval.c positionid.c matchid.c matchequity.c
    bearoff.c bearoffgammon.c dice.c mtsupport.c
    util.c osr.c mec.c rollout.c multithread.c
    drawboard.c evallock.c
)
SRC_PATHS=()
for src in "${GNUBG_SOURCES[@]}"; do SRC_PATHS+=("$GNUBG_DIR/$src"); done

cd "$NATIVE_DIR"

# gmp ships no pkg-config file, so unlike glib it cannot be discovered -- and on
# macOS Homebrew's prefix is not a default compiler search path, so it has to be
# named. Empty on Linux and Windows, where the toolchain already looks in the
# right places. The -L lands before -lgmp at the end of COMMON_FLAGS, which is
# where a linker needs it.
#
# -shared is NOT here any more: it is the one flag whose spelling differs per
# object format, so it belongs with the per-platform link invocation below.
PLATFORM_FLAGS=()
if [ "$TARGET_OS" = "macos" ]; then
    PLATFORM_FLAGS=(-I"$GMP_PREFIX/include" -L"$GMP_PREFIX/lib")
fi

COMMON_FLAGS=(
    -O2
    -I "$NATIVE_DIR"
    -I "$GNUBG_DIR"
    -I "$GNUBG_DIR/lib"
    "${PLATFORM_FLAGS[@]}"
    $(pkg-config --cflags glib-2.0 gthread-2.0 gobject-2.0)
    -DGNUBGAPI_BUILD -DHAVE_CONFIG_H
    -DAC_DOCDIR='"."' -DAC_DATADIR='"."' -DAC_PKGDATADIR='"."'
    "$NATIVE_DIR/gnubgapi.c"
    "${SRC_PATHS[@]}"
    "$GNUBG_DIR/lib/.libs/libevent.a"
    $(pkg-config --libs glib-2.0 gthread-2.0 gobject-2.0)
    -lgmp -lm
)

# A case, not an if/else: when macOS was added, `else` silently meant "Windows
# or anything new", so a third platform would have been handed MinGW linker
# flags and blamed for the failure.
case "$TARGET_OS" in
    linux)
        gcc -fPIC -shared "${COMMON_FLAGS[@]}" -o "$OUT_DIR/$OUT_NAME"
        ;;
    macos)
        # -dynamiclib rather than -shared: clang accepts both, but only this is
        # the documented spelling for a Mach-O shared library, and the two are
        # not synonyms for every flag combination.
        #
        # No -install_name is set, deliberately. An install_name is what a
        # linker bakes into anything that links against this library at build
        # time, and nothing does: the daemon dlopens it by an absolute path
        # beside its own executable (gnubg-engine-server src/main.rs), so the
        # field is never consulted. Setting @rpath here would be cargo-culted
        # ceremony that implies a load path we do not use.
        #
        # Symbol export needs no flag either -- clang exports every symbol with
        # default visibility from a dylib, which is what --export-all-symbols
        # buys explicitly on the MinGW side.
        clang -dynamiclib "${COMMON_FLAGS[@]}" -o "$OUT_DIR/$OUT_NAME"
        ;;
    windows)
        # MinGW64: -fPIC is implicit on Windows DLLs and emits a noise warning.
        # We also want an import library and explicit symbol export.
        gcc -shared "${COMMON_FLAGS[@]}" \
            -Wl,--out-implib,"$OUT_DIR/libgnubgapi.dll.a" \
            -Wl,--export-all-symbols \
            -o "$OUT_DIR/$OUT_NAME"
        ;;
    *)
        echo "ERROR: no link step for TARGET_OS '$TARGET_OS'" >&2
        exit 1
        ;;
esac

echo "==> Done: $OUT_DIR/$OUT_NAME"
