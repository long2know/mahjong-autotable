using System.Text.Json;
using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Tests.Changsha._TestHarness;

namespace Mahjong.Autotable.Api.Tests.Autotable;

/// <summary>
/// CAT-PHASE5A: Translator unit tests. The translator is a pure function over
/// <see cref="ChangshaGameState"/>; these tests assert the shape and contents of
/// the snapshot emitted to the upstream autotable bundle.
/// </summary>
public class AutotableTranslatorTests
{
    // ── upstream typeIndex mapping ────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void UpstreamTypeIndex_Matches_TileIdDivBy4_ForAllTiles()
    {
        for (var tileId = 0; tileId < 108; tileId++)
        {
            Assert.Equal(tileId / 4, AutotableSlotMap.UpstreamTypeIndex(tileId));
        }
    }

    [Fact, Trait("Category", "Phase5a")]
    public void UpstreamTypeIndex_Throws_OutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AutotableSlotMap.UpstreamTypeIndex(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutotableSlotMap.UpstreamTypeIndex(108));
    }

    // ── wall slot generation ──────────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void WallStackCount_Is_14_14_13_13()
    {
        Assert.Equal(14, AutotableSlotMap.WallStackCount(0));
        Assert.Equal(14, AutotableSlotMap.WallStackCount(1));
        Assert.Equal(13, AutotableSlotMap.WallStackCount(2));
        Assert.Equal(13, AutotableSlotMap.WallStackCount(3));
    }

    [Fact, Trait("Category", "Phase5a")]
    public void EnumerateWallSlotsInOrder_Yields_108_Unique_Slots()
    {
        var slots = AutotableSlotMap.EnumerateWallSlotsInOrder()
            .Select(t => AutotableSlotMap.WallSlot(t.Seat, t.Col, t.Layer))
            .ToList();

        Assert.Equal(108, slots.Count);
        Assert.Equal(108, slots.Distinct().Count());

        var bySeat = AutotableSlotMap.EnumerateWallSlotsInOrder().GroupBy(t => t.Seat).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(28, bySeat[0]);
        Assert.Equal(28, bySeat[1]);
        Assert.Equal(26, bySeat[2]);
        Assert.Equal(26, bySeat[3]);
    }

    [Fact, Trait("Category", "Phase5a")]
    public void WallSlot_Rejects_OutOfRange_Cols_PerSeat()
    {
        // Seat 0 has 14 cols → col 13 is valid, col 14 is not.
        Assert.Equal("wall.13.0@0", AutotableSlotMap.WallSlot(0, 13, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutotableSlotMap.WallSlot(0, 14, 0));

        // Seat 2 has 13 cols → col 12 is valid, col 13 is not.
        Assert.Equal("wall.12.1@2", AutotableSlotMap.WallSlot(2, 12, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => AutotableSlotMap.WallSlot(2, 13, 0));
    }

    // ── JOINED snapshot shape ─────────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_Contains_108_Things()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);

        var things = entries.Where(e => e.Kind == "things").ToList();
        Assert.Equal(108, things.Count);
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_Contains_4_Seats_And_4_Nicks()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);

        Assert.Equal(4, entries.Count(e => e.Kind == "seats"));
        Assert.Equal(4, entries.Count(e => e.Kind == "nicks"));
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_Contains_1_Match_Entry()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);

        var matchEntries = entries.Where(e => e.Kind == "match").ToList();
        Assert.Single(matchEntries);
        Assert.Equal(0L, Convert.ToInt64(matchEntries[0].Key));
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_Contains_1_Dice_Entry_With_2_Dice()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);

        var dice = entries.Where(e => e.Kind == "dice").ToList();
        Assert.Single(dice);

        // Round-trip through JSON to verify the dice value is a 2-element array
        // and state is "rolled" (per upstream DiceInfo shape).
        var json = JsonSerializer.Serialize(dice[0].Value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        var diceArr = doc.RootElement.GetProperty("dice");
        Assert.Equal(JsonValueKind.Array, diceArr.ValueKind);
        Assert.Equal(2, diceArr.GetArrayLength());
        Assert.Equal("rolled", doc.RootElement.GetProperty("state").GetString());
    }

    // ── slot name correctness ─────────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_WallSlots_AreUnique_AndUseCanonicalNames()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var slotNames = entries
            .Where(e => e.Kind == "things")
            .Select(e => ExtractSlotName(e.Value!))
            .ToList();

        Assert.Equal(slotNames.Count, slotNames.Distinct().Count());
        var wallSlots = slotNames.Where(s => s.StartsWith("wall.")).ToList();

        // Each wall slot must be a valid 14/14/13/13 layout name.
        foreach (var s in wallSlots)
        {
            // Format: wall.{col}.{layer}@{seat}
            var parts = s.Split('.');
            Assert.Equal(3, parts.Length);
            var seat = int.Parse(parts[2].Split('@')[1]);
            var col = int.Parse(parts[1]);
            var layer = int.Parse(parts[2].Split('@')[0]);
            Assert.InRange(col, 0, AutotableSlotMap.WallStackCount(seat) - 1);
            Assert.InRange(layer, 0, 1);
        }
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_HandSlots_Sized13Or14PerSeat()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var slotNames = entries
            .Where(e => e.Kind == "things")
            .Select(e => ExtractSlotName(e.Value!))
            .ToList();

        var bySeat = new Dictionary<int, int>();
        foreach (var s in slotNames.Where(n => n.StartsWith("hand.")))
        {
            var seat = int.Parse(s.Split('@')[1]);
            bySeat[seat] = bySeat.GetValueOrDefault(seat) + 1;
        }

        Assert.Equal(14, bySeat[state.DealerSeatIndex]);
        for (var i = 0; i < 4; i++)
            if (i != state.DealerSeatIndex)
                Assert.Equal(13, bySeat[i]);
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterStartGame_TotalWallThings_Equal_55()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var wallCount = entries
            .Where(e => e.Kind == "things")
            .Count(e => ExtractSlotName(e.Value!).StartsWith("wall."));
        Assert.Equal(55, wallCount);
    }

    // ── discard slot mapping ──────────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void DiscardSlot_Names_FollowDocumentedShape()
    {
        Assert.Equal("discard.0.0@0", AutotableSlotMap.DiscardSlot(0, 0, 0));
        Assert.Equal("discard.2.5@3", AutotableSlotMap.DiscardSlot(3, 2, 5));
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_AfterDiscard_TileMovesFromHandToDiscard()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var dealer = state.DealerSeatIndex;
        var tile = state.Hands[dealer].ConcealedTiles.First();
        ChangshaGameStateMachine.Discard(state, dealer, tile);

        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: dealer);
        var slotByTile = entries
            .Where(e => e.Kind == "things")
            .ToDictionary(e => Convert.ToInt32(e.Key), e => ExtractSlotName(e.Value!));

        // Discarded tile must now sit in seat-N's discard.0.0 slot.
        Assert.Equal(AutotableSlotMap.DiscardSlot(dealer, 0, 0), slotByTile[tile]);
        // No hand slot still references the discarded tile.
        Assert.DoesNotContain(slotByTile.Values, s => s == AutotableSlotMap.HandSlot(dealer, 0) && slotByTile.ContainsKey(tile) == false);
    }

    // ── meld slot mapping ─────────────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_WithPungMeld_Produces_3_MeldEntries_At_Meld0()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var seat = (state.DealerSeatIndex + 1) % 4;
        // Inject a synthetic pung meld on seat N — 3 tiles of 1-tong.
        state.Hands[seat].Melds.Add(new Meld
        {
            Kind = MeldKind.Pung,
            TileIds = new List<int>
            {
                ChangshaTestHelpers.Tid(Suit.Tong, 1, 0),
                ChangshaTestHelpers.Tid(Suit.Tong, 1, 1),
                ChangshaTestHelpers.Tid(Suit.Tong, 1, 2)
            },
            ClaimedFromSeatIndex = state.DealerSeatIndex
        });
        // Also remove these tiles from anywhere else they exist, to keep totals at 108.
        foreach (var id in state.Hands[seat].Melds[^1].TileIds)
        {
            foreach (var h in state.Hands)
                h.ConcealedTiles.Remove(id);
            state.Wall.Remove(id);
        }

        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var meldEntries = entries
            .Where(e => e.Kind == "things")
            .Where(e => ExtractSlotName(e.Value!).StartsWith($"meld.0."))
            .Where(e => ExtractSlotName(e.Value!).EndsWith($"@{seat}"))
            .ToList();

        Assert.Equal(3, meldEntries.Count);
        foreach (var e in meldEntries)
            Assert.Equal(0, ExtractRotationIndex(e.Value!)); // FACE_UP
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Snapshot_WithConcealedKong_Produces_4_FaceDown_MeldEntries()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var seat = (state.DealerSeatIndex + 1) % 4;
        // Inject a concealed kong (meld index 0) — 4 tiles of 1-wan.
        state.Hands[seat].Melds.Add(new Meld
        {
            Kind = MeldKind.ConcealedKong,
            TileIds = new List<int>
            {
                ChangshaTestHelpers.Tid(Suit.Wan, 1, 0),
                ChangshaTestHelpers.Tid(Suit.Wan, 1, 1),
                ChangshaTestHelpers.Tid(Suit.Wan, 1, 2),
                ChangshaTestHelpers.Tid(Suit.Wan, 1, 3)
            }
        });
        foreach (var id in state.Hands[seat].Melds[^1].TileIds)
        {
            foreach (var h in state.Hands)
                h.ConcealedTiles.Remove(id);
            state.Wall.Remove(id);
        }

        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);
        var meldEntries = entries
            .Where(e => e.Kind == "things")
            .Where(e => ExtractSlotName(e.Value!).StartsWith($"meld.0."))
            .Where(e => ExtractSlotName(e.Value!).EndsWith($"@{seat}"))
            .ToList();

        Assert.Equal(4, meldEntries.Count);
        // Concealed kong → rotationIndex 2 (FACE_DOWN per upstream meld slot rotations).
        foreach (var e in meldEntries)
            Assert.Equal(2, ExtractRotationIndex(e.Value!));
    }

    // ── always-available pattern ──────────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void Translate_WithNullState_ReturnsOnlyMatchEntry()
    {
        var entries = ChangshaToAutotableTranslator.Translate(null);
        Assert.Single(entries);
        Assert.Equal("match", entries[0].Kind);
    }

    [Fact, Trait("Category", "Phase5a")]
    public void Translate_MatchEntry_ForcesFives000_ForCleanTypeIndexMapping()
    {
        var entries = ChangshaToAutotableTranslator.Translate(null);
        var match = entries.Single(e => e.Kind == "match");
        var json = JsonSerializer.Serialize(match.Value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("000", doc.RootElement.GetProperty("conditions").GetProperty("fives").GetString());
        Assert.Equal("FOUR_PLAYER", doc.RootElement.GetProperty("conditions").GetProperty("gameType").GetString());
    }

    // ── viewer-seat hand visibility ───────────────────────────────────

    [Fact, Trait("Category", "Phase5a")]
    public void HandTiles_AreFaceUp_ForViewerSeat_AndFaceDown_ForOthers()
    {
        var state = ChangshaTestHelpers.NewGameDealtTo(seed: 7);
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 0);

        foreach (var e in entries.Where(e => e.Kind == "things"))
        {
            var slot = ExtractSlotName(e.Value!);
            if (!slot.StartsWith("hand.")) continue;
            var seat = int.Parse(slot.Split('@')[1]);
            var rot = ExtractRotationIndex(e.Value!);
            if (seat == 0)
                Assert.Equal(1, rot); // FACE_UP
            else
                Assert.Equal(2, rot); // FACE_DOWN
        }
    }

    // ── helpers ───────────────────────────────────────────────────────

    private static string ExtractSlotName(object value)
    {
        var json = JsonSerializer.Serialize(value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("slotName").GetString()!;
    }

    private static int ExtractRotationIndex(object value)
    {
        var json = JsonSerializer.Serialize(value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("rotationIndex").GetInt32();
    }

    // ── claim window deadline plumbing (Frost 2026-05-29) ─────────────
    //
    // When the runtime opens a claim window the state machine stamps
    // <c>ChangshaClaimWindow.OpenedAtUnixMs</c>.  The translator must surface
    // an absolute deadline = OpenedAtUnixMs + claimWindowTimeoutMs so the
    // autotable overlay / side-panel countdown renders meaningfully instead
    // of treating <c>deadline=0</c> as "already expired" and auto-passing.
    // Falls back to 0 when the caller doesn't supply a timeout (back-compat).

    [Fact, Trait("Category", "Phase5a")]
    public void ClaimEntry_EmitsAbsoluteDeadline_WhenTimeoutPassed()
    {
        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = 0,
                DiscardTileId = 12,
                OpenedAtUnixMs = 1_700_000_000_000L,
                Opportunities = [
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 1,
                        ClaimType = Tables.TableClaimType.Pung,
                        Priority = 1
                    }
                ]
            }
        };
        for (var i = 0; i < 4; i++)
        {
            state.Seats.Add(new ChangshaSeatState { SeatIndex = i });
            state.Hands.Add(new ChangshaHandState { SeatIndex = i });
        }

        var entries = ChangshaToAutotableTranslator.Translate(
            state, viewerSeat: 1, claimWindowTimeoutMs: 5000);

        var claim = entries.Single(e => e.Kind == "claim");
        var json = JsonSerializer.Serialize(claim.Value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        // deadline = 1_700_000_000_000 + 5000 = 1_700_000_005_000
        Assert.Equal(1_700_000_005_000L, doc.RootElement.GetProperty("deadline").GetInt64());
    }

    [Fact, Trait("Category", "Phase5a")]
    public void ClaimEntry_EmitsZeroDeadline_WhenTimeoutNotPassed()
    {
        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = 0,
                DiscardTileId = 12,
                OpenedAtUnixMs = 1_700_000_000_000L,
                Opportunities = [
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 1,
                        ClaimType = Tables.TableClaimType.Pung,
                        Priority = 1
                    }
                ]
            }
        };
        for (var i = 0; i < 4; i++)
        {
            state.Seats.Add(new ChangshaSeatState { SeatIndex = i });
            state.Hands.Add(new ChangshaHandState { SeatIndex = i });
        }

        // Caller didn't supply a timeout — preserve the legacy contract
        // (0 = "no client timer; server enforces").
        var entries = ChangshaToAutotableTranslator.Translate(state, viewerSeat: 1);

        var claim = entries.Single(e => e.Kind == "claim");
        var json = JsonSerializer.Serialize(claim.Value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0L, doc.RootElement.GetProperty("deadline").GetInt64());
    }

    [Fact, Trait("Category", "Phase5a")]
    public void ClaimEntry_EmitsZeroDeadline_WhenOpenedAtZero_EvenWithTimeout()
    {
        // Rehydrated state from before OpenedAtUnixMs existed — translator
        // must NOT compute a bogus deadline = 0 + timeout, since that's a
        // 1970 epoch value that fails the "is this still open" check.
        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = 0,
                DiscardTileId = 12,
                OpenedAtUnixMs = 0L, // rehydrated / legacy
                Opportunities = [
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 1,
                        ClaimType = Tables.TableClaimType.Pung,
                        Priority = 1
                    }
                ]
            }
        };
        for (var i = 0; i < 4; i++)
        {
            state.Seats.Add(new ChangshaSeatState { SeatIndex = i });
            state.Hands.Add(new ChangshaHandState { SeatIndex = i });
        }

        var entries = ChangshaToAutotableTranslator.Translate(
            state, viewerSeat: 1, claimWindowTimeoutMs: 5000);

        var claim = entries.Single(e => e.Kind == "claim");
        var json = JsonSerializer.Serialize(claim.Value, AutotableJson.Options);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0L, doc.RootElement.GetProperty("deadline").GetInt64());
    }

    [Fact, Trait("Category", "Phase5a")]
    public void ClaimEntry_EmitsOnePerEligibleSeat_KeyedBySeatIndex()
    {
        // Per Changsha spec §3.3 every eligible seat (not just the priority
        // winner) gets its own claim entry, keyed by that seat's index.  The
        // frontend overlay matches `key === String(selfSeat)` to decide
        // whether to surface the window; missing entries = no overlay.
        var state = new ChangshaGameState
        {
            Phase = ChangshaPhase.AwaitingClaim,
            ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = 0,
                DiscardTileId = 12,
                OpenedAtUnixMs = 1_700_000_000_000L,
                Opportunities = [
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 1, ClaimType = Tables.TableClaimType.Chow, Priority = 1
                    },
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 2, ClaimType = Tables.TableClaimType.Pung, Priority = 2
                    },
                    new ChangshaClaimOpportunity
                    {
                        SeatIndex = 3, ClaimType = Tables.TableClaimType.Hu, Priority = 3
                    }
                ]
            }
        };
        for (var i = 0; i < 4; i++)
        {
            state.Seats.Add(new ChangshaSeatState { SeatIndex = i });
            state.Hands.Add(new ChangshaHandState { SeatIndex = i });
        }

        var entries = ChangshaToAutotableTranslator.Translate(
            state, viewerSeat: 1, claimWindowTimeoutMs: 5000);

        var claims = entries.Where(e => e.Kind == "claim").ToList();
        Assert.Equal(3, claims.Count);
        // Frost 2026-05-29 — keys are stringified to match the frontend
        // Collection's Map<string, V> storage convention (game-ui.ts writes
        // `client.claim.set(String(selfSeat), …)` locally; the server must
        // use the same key shape so updates merge into a single entry).
        Assert.Contains(claims, c => c.Key.Equals("1"));
        Assert.Contains(claims, c => c.Key.Equals("2"));
        Assert.Contains(claims, c => c.Key.Equals("3"));
        // Discarder (seat 0) never gets a claim entry.
        Assert.DoesNotContain(claims, c => c.Key.Equals("0"));
    }
}
