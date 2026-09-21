# HOLD: externally changed room-recovery source does not match published final pin

September14,2026. While answering Hudson's request for the exact corrected restart pin, an actual current hash comparison failed. **No production edit/revert/build/test/re-review was performed in response.** Coordinator must reconcile the new delta's ownership/review and freeze inputs before a fresh independent current-tree replay.

The already implemented durable-recovery pin remains in `sessions/2026-09-14-bishop-final-backend-closeout.md` and `backend-final-current-profile-v2/final-manifest.json` SHAca114063c56eab4ce2349c7590e5ce641bbbb95dac79543854446c4541f97990. Its coherent269-case run includes Hudson's exact unchanged public-room test b2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5 through progressed-state/same-runtime/zero-new-row and all owner/observer/cross-room/continued-discard suffix assertions. The original baseline RED on Runtime5491877b…/Endpoint8cd5b64b… remains genuine; it is not relabeled.

## Actual current drift (2 of28 owned paths)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.PublicRooms.cs`: published c3d773921e0e4494d3b65e72e2695c487b9a822df3307d10941fe64fea3b7c03 -> current948237a651beabfde4d474e277776e2b68fe0fb209f8f9f026f87549251d2cbd. Delta adds recovered-structure validation and calls it before constructing a recovered instance.
- `docs/public-room-recovery.md`: published113928d7b1f2205c86702b152aa0be60b43d3199dfebeee973573fceec0922e8 -> current26924f230b75cb67e689a5a311a672f39bd8121e643f1909ac2b3621f9c12489.

Bishop did not make these newly observed edits in this session and does not infer their author, approval or rejection status. They are preserved, not overwritten. All other28-manifest owned-source paths match; main Runtime remains14b12539… and the public-room test remainsb2d14d34… unchanged. A source-diff read identified the delta only; it is NOT independent review or execution of the new bytes.

Hudson was notified not to label current working-tree recovery inputs as frozen under the older manifest. The known corrected pin can be reviewed as its own immutable artifact; a current-tree run requires a new coordinated pin including these changes. No source/image/cohort credit or legacy alias/seed/credential substitution is authorized by this record.
