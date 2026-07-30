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
        *) echo "ERROR: unrecognised host $(uname -s); set GNUBGAPI_TARGET" >&2; exit 1 ;;
    esac
fi

# TARGET_OS, not TARGET, is what the compiler flags below branch on. Every
# difference between these builds is ELF-versus-PE — position independence,
# import libraries, symbol export — and none of it is about the instruction
# set. Branching on the full RID made the two look like x86 decisions and was
# the only thing standing between this script and an arm64 build.
case "$TARGET" in
    linux-x64|linux-arm64) OUT_NAME="libgnubgapi.so";  TARGET_OS="linux" ;;
    win-x64)               OUT_NAME="libgnubgapi.dll"; TARGET_OS="windows" ;;
    *) echo "ERROR: unsupported target '$TARGET'" >&2; exit 1 ;;
esac
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
# Inject CFLAGS at configure time so gnubg's Makefiles bake it in everywhere
# instead of trying to override per-target later.
CFG_CFLAGS="-O3 -ffast-math"
if [ "$TARGET_OS" = "linux" ]; then CFG_CFLAGS="$CFG_CFLAGS -fPIC"; fi
CFLAGS="$CFG_CFLAGS" ./configure --without-python --without-gtk --without-board3d --quiet

# Step 2: build gnubg's lib/libevent.la — the only internal static lib our
# wrapper transitively depends on (queues + event loop helpers used by
# multithread.c). We do NOT run `make` at the gnubg root; that would build
# the full app, which we don't need and which has dependencies (gtk, png,
# freetype, ...) that aren't relevant for an eval-only library.
echo "==> Building gnubg lib/"
make -C lib -j"$(nproc)"

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
COMMON_FLAGS=(
    -shared -O2
    -I "$NATIVE_DIR"
    -I "$GNUBG_DIR"
    -I "$GNUBG_DIR/lib"
    $(pkg-config --cflags glib-2.0 gthread-2.0 gobject-2.0)
    -DGNUBGAPI_BUILD -DHAVE_CONFIG_H
    -DAC_DOCDIR='"."' -DAC_DATADIR='"."' -DAC_PKGDATADIR='"."'
    "$NATIVE_DIR/gnubgapi.c"
    "${SRC_PATHS[@]}"
    "$GNUBG_DIR/lib/.libs/libevent.a"
    $(pkg-config --libs glib-2.0 gthread-2.0 gobject-2.0)
    -lgmp -lm
)

if [ "$TARGET_OS" = "linux" ]; then
    gcc -fPIC "${COMMON_FLAGS[@]}" -o "$OUT_DIR/$OUT_NAME"
else
    # MinGW64: -fPIC is implicit on Windows DLLs and emits a noise warning.
    # We also want an import library and explicit symbol export.
    gcc "${COMMON_FLAGS[@]}" \
        -Wl,--out-implib,"$OUT_DIR/libgnubgapi.dll.a" \
        -Wl,--export-all-symbols \
        -o "$OUT_DIR/$OUT_NAME"
fi

echo "==> Done: $OUT_DIR/$OUT_NAME"
