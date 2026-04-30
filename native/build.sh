#!/usr/bin/env bash
# Build libgnubgapi.so on Linux.
#
# Reads gnubg sources from the pinned submodule at native/gnubg/.
# Does NOT edit anything inside the submodule — it just compiles against it.
# Output goes to ../runtimes/linux-x64/native/libgnubgapi.so by default.
set -euo pipefail

NATIVE_DIR="$(cd "$(dirname "$0")" && pwd)"
GNUBG_DIR="$NATIVE_DIR/gnubg"

# Target detection: GNUBGAPI_TARGET overrides; otherwise infer from `uname`.
# Windows builds run under MSYS2/MinGW64 and uname reports MINGW64_NT-*.
TARGET="${GNUBGAPI_TARGET:-}"
if [ -z "$TARGET" ]; then
    case "$(uname -s)" in
        MINGW64_NT*|MSYS_NT*|CYGWIN_NT*) TARGET="win-x64" ;;
        Linux)                            TARGET="linux-x64" ;;
        *) echo "ERROR: unrecognised host $(uname -s); set GNUBGAPI_TARGET" >&2; exit 1 ;;
    esac
fi
case "$TARGET" in
    linux-x64) OUT_NAME="libgnubgapi.so" ;;
    win-x64)   OUT_NAME="libgnubgapi.dll" ;;
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
if [ "$TARGET" = "linux-x64" ]; then CFG_CFLAGS="$CFG_CFLAGS -fPIC"; fi
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

if [ "$TARGET" = "linux-x64" ]; then
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
