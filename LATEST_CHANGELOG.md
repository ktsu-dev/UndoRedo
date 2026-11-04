## v3.5.0 (minor)

Changes since v3.4.0:

- Report a re-executed command only once it has left the history [patch] (#171) (matt-edmondson)
- Add a SessionStart hook for Claude Code cloud sessions (#156) (matt-edmondson)
- Leave originals a merged composite still holds out of CommandsDiscarded [patch] (#169) (matt-edmondson)
- Undo the last command before merging, so an in-place MergeWith works [patch] (#168) (matt-edmondson)
- Fix the open history bugs and the documented examples [minor] (Claude)
- Re-save the existing boundary when MarkAsSaved repeats at one position [patch] (Claude)
- Always write InitialStateIsClean and ChangeMetadata.Size [patch] (Claude)

