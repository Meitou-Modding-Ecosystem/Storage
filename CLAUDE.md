# Agent instructions

A [MeitouClient](https://github.com/Meitou-Modding-Ecosystem/MeitouClient) code mod repo in the Meitou-Modding-Ecosystem org. GPL-3.0-or-later.
`CLAUDE.md` is canonical; keep `AGENTS.md` identical. Everything above `## This repo

- Storage (`Storage/`, mod id `storage`, slug `storage`; docs `docs/storage.md`): the storehouse building (`Storage/Structures/storehouse.glb`,
  records added in the data phase, `Storage.mod` ships empty), its filters, hauler job and hauling network. No frameworks: `engine` follows the
  newest upstream tag (`engine-update.yml`).
- Opening a storehouse from range and opening a container on arrival use engine calls found by reflection (`Storage/Ui/StorageWindows.cs`);
  until the engine has them the mod falls back to a move order.
