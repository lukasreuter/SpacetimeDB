#!/bin/bash

cargo build --locked --release -p spacetimedb-standalone -p spacetimedb-update -p spacetimedb-cli

mkdir -p ~/.local/bin
STDB_VERSION="$(./target/release/spacetimedb-cli --version | sed -n 's/.*spacetimedb tool version \([0-9.]*\);.*/\1/p')"
STDB_VERSION="0.0.1"
echo $STDB_VERSION
mkdir -p ~/.local/share/spacetime/bin/$STDB_VERSION

cp target/release/spacetimedb-update ~/.local/bin/spacetime
cp target/release/spacetimedb-cli ~/.local/share/spacetime/bin/$STDB_VERSION
cp target/release/spacetimedb-standalone ~/.local/share/spacetime/bin/$STDB_VERSION

# Add to your shell config if not already present:
#export PATH="$HOME/.local/bin:$PATH"

# Set the active version:
spacetime version use $STDB_VERSION