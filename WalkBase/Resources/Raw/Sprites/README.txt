Drop building sprite PNGs in THIS folder to replace the built-in vector art.

The app loads "Sprites/<name>.png" on demand (SpriteCache). Any building whose PNG is
present renders the image instead of the procedural shape; anything missing falls back to
the vector drawing — so you can add art one building (or one level) at a time.

See SPRITES.md in the project root for the full filename list and sizing guide.
