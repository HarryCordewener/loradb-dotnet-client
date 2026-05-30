#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Build lora_ffi from the upstream lora-db/lora monorepo.

Usage:
  scripts/build-lora-ffi.sh [options]

Options:
  --ref <git-ref>       Upstream ref/tag/commit to build (default: value in LoraDb.Client.Native/lora-ffi.version)
  --target <triple>     Cargo target triple (e.g. x86_64-unknown-linux-gnu)
  --out <path>          Output path for the built native library
  --repo-url <url>      Upstream repository URL (default: https://github.com/lora-db/lora.git)
  --update-pin          Persist --ref into LoraDb.Client.Native/lora-ffi.version
  --keep-workdir        Keep the temporary build directory for inspection
  -h, --help            Show this help
EOF
}

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
pin_file="$repo_root/LoraDb.Client.Native/lora-ffi.version"

ref=""
target=""
out_path=""
repo_url="https://github.com/lora-db/lora.git"
update_pin=false
keep_workdir=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --ref)
      ref="${2:-}"
      shift 2
      ;;
    --target)
      target="${2:-}"
      shift 2
      ;;
    --out)
      out_path="${2:-}"
      shift 2
      ;;
    --repo-url)
      repo_url="${2:-}"
      shift 2
      ;;
    --update-pin)
      update_pin=true
      shift
      ;;
    --keep-workdir)
      keep_workdir=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$ref" ]]; then
  if [[ ! -f "$pin_file" ]]; then
    echo "Missing pin file: $pin_file" >&2
    exit 1
  fi
  ref="$(tr -d '[:space:]' < "$pin_file")"
fi

if [[ -z "$ref" ]]; then
  echo "Resolved empty upstream ref. Set --ref or update $pin_file." >&2
  exit 1
fi

triple_to_filename() {
  local triple="$1"
  if [[ "$triple" == *windows* ]]; then
    echo "lora_ffi.dll"
  elif [[ "$triple" == *darwin* || "$triple" == *apple* ]]; then
    echo "liblora_ffi.dylib"
  else
    echo "liblora_ffi.so"
  fi
}

triple_to_rid() {
  local triple="$1"
  case "$triple" in
    x86_64-unknown-linux-gnu) echo "linux-x64" ;;
    aarch64-unknown-linux-gnu) echo "linux-arm64" ;;
    x86_64-apple-darwin) echo "osx-x64" ;;
    aarch64-apple-darwin) echo "osx-arm64" ;;
    x86_64-pc-windows-msvc) echo "win-x64" ;;
    *) return 1 ;;
  esac
}

if [[ -z "$target" ]]; then
  target="$(rustc -vV | awk '/^host: /{print $2}')"
fi

native_filename="$(triple_to_filename "$target")"

if [[ -z "$out_path" ]]; then
  if rid="$(triple_to_rid "$target")"; then
    out_path="$repo_root/LoraDb.Client.Native/runtimes/$rid/native/$native_filename"
  else
    echo "Unsupported target triple '$target' for automatic output path." >&2
    echo "Use --out <path> explicitly." >&2
    exit 1
  fi
fi

tmp_dir="$(mktemp -d)"
cleanup() {
  if [[ "$keep_workdir" == true ]]; then
    echo "Keeping temporary directory: $tmp_dir"
  else
    rm -rf "$tmp_dir"
  fi
}
trap cleanup EXIT

echo "Using upstream ref: $ref"
echo "Using cargo target: $target"
echo "Cloning: $repo_url"

git clone "$repo_url" "$tmp_dir/lora"
pushd "$tmp_dir/lora" >/dev/null
git checkout "$ref"

ffi_crate_dir="$tmp_dir/lora/crates/bindings/lora-ffi"
if [[ ! -d "$ffi_crate_dir" ]]; then
  echo "Could not find crate directory: $ffi_crate_dir" >&2
  exit 1
fi

cargo build --release --target "$target" --manifest-path "$ffi_crate_dir/Cargo.toml"
popd >/dev/null

artifact="$tmp_dir/lora/target/$target/release/$native_filename"
if [[ ! -f "$artifact" ]]; then
  echo "Built artifact not found: $artifact" >&2
  exit 1
fi

mkdir -p "$(dirname "$out_path")"
cp "$artifact" "$out_path"
echo "Copied artifact to: $out_path"

if [[ "$update_pin" == true ]]; then
  printf '%s\n' "$ref" > "$pin_file"
  echo "Updated pin file: $pin_file"
fi
