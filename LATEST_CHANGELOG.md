## v3.5.0 (minor)

Changes since v3.4.0:

- Reload a description-less save point under RespectRequiredConstructorParameters [patch] (#189) (matt-edmondson)
- Report a dropped command only once it has left the history [patch] (#190) (matt-edmondson)
- Restore navigation context and metadata on commands that implement ICommand directly [minor] (#191) (matt-edmondson)
- Report placeholders in the state snapshot's CanUndo/CanRedo and in GetCommandsToUndo [patch] (#194) (matt-edmondson)
- Ask CanMergeWith once, and drop a merged-into command a failed merge leaves unapplied [minor] (#176) (matt-edmondson)
- Report handler failures when a command fails partway through a revert [patch] (#179) (matt-edmondson)
- Keep a command a composite in the history holds out of CommandsDiscarded [patch] (#181) (matt-edmondson)
- Apply a mergeable edit on its own when the previous command can't be undone to merge [patch] (#182) (matt-edmondson)
- Trim a restored history to MaxStackSize [patch] (Claude)
- Report a re-executed command only once it has left the history [patch] (#171) (matt-edmondson)
- Add a SessionStart hook for Claude Code cloud sessions (#156) (matt-edmondson)
- Leave originals a merged composite still holds out of CommandsDiscarded [patch] (#169) (matt-edmondson)
- Undo the last command before merging, so an in-place MergeWith works [patch] (#168) (matt-edmondson)
- Fix the open history bugs and the documented examples [minor] (Claude)
- Re-save the existing boundary when MarkAsSaved repeats at one position [patch] (Claude)
- Always write InitialStateIsClean and ChangeMetadata.Size [patch] (Claude)

