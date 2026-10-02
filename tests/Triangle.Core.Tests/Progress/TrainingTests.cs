using Triangle.Core.Combat;
using Triangle.Core.Data;
using Triangle.Core.Progress;
using Triangle.Core.Units;

namespace Triangle.Core.Tests.Progress;

public class TrainingTests
{
    // archery: 랭크 1, 민첩/근력. rapid: 랭크 2, archery 2 필요. heavy: 랭크 1, 체력/체력.
    private static readonly GameData Data = GameDataLoader.Parse(
        """
        [ { "id": "archery", "name": "활 숙련", "rank": 1, "primary": "Dex", "secondary": "Str" },
          { "id": "rapid", "name": "속사", "rank": 2, "primary": "Dex", "secondary": "Speed",
            "prerequisites": [ { "skillId": "archery", "level": 2 } ] },
          { "id": "heavy", "name": "체력 단련", "rank": 1, "primary": "Vital", "secondary": "Vital" } ]
        """,
        """[ { "id": "strike", "name": "공격" } ]""",
        """[ { "id": "camp", "name": "야영지", "units": [ { "id": "e", "name": "적", "row": "Front", "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 } } ] } ]""");

    /// <summary>모든 스탯 15 → 배율 100%.</summary>
    private static PartyMember Member(Stats? stats = null, Dictionary<string, int>? sp = null) =>
        new("m", "멤버", stats ?? new Stats(15, 15, 15, 15, 15), Row.Front, sp ?? [], []);

    private static void Enqueue(PartyMember m, string skillId) =>
        Assert.True(Training.Enqueue(m, skillId, Data, out _), $"enqueue {skillId}");

    [Fact]
    public void Multiplier_weights_primary_twice_secondary_once()
    {
        Assert.Equal(100, Training.MultiplierPercent(Member(), Data.Skills["archery"]));
        // (민첩 24×2 + 근력 9) / 45 = 126%
        Assert.Equal(126, Training.MultiplierPercent(Member(new Stats(9, 24, 15, 15, 15)), Data.Skills["archery"]));
    }

    [Fact]
    public void Grant_fills_queue_in_order_and_carries_over_remainder()
    {
        var m = Member();
        Enqueue(m, "archery"); // Lv1 = 250
        Enqueue(m, "archery"); // Lv2 = 1,415

        var result = Training.Grant(m, 1_000, Data);

        Assert.Equal([new LevelUp("m", "archery", 1)], result.LevelUps);
        Assert.Equal(1_000, m.SkillPoints["archery"]);
        Assert.Equal([new TrainingQueueEntry("archery", 2)], m.TrainingQueue);
        Assert.Equal(0, m.UnallocatedSp);
    }

    [Fact]
    public void Grant_keeps_leftover_as_unallocated_when_queue_runs_out()
    {
        var m = Member();
        Enqueue(m, "archery");

        var result = Training.Grant(m, 1_000, Data);

        Assert.Equal(250, m.SkillPoints["archery"]);
        Assert.Empty(m.TrainingQueue);
        Assert.Equal(750, m.UnallocatedSp);
        Assert.Equal(250, result.Applied);
    }

    [Fact]
    public void Multiplier_changes_how_much_raw_sp_a_level_costs()
    {
        // 배율 166%: 250 SP를 채우는 데 원래 SP 151이 든다 (올림).
        var m = Member(new Stats(15, 30, 15, 15, 15)); // (30×2 + 15)/45 = 166%
        Enqueue(m, "archery");

        Training.Grant(m, 1_000, Data);

        Assert.Equal(250, m.SkillPoints["archery"]);
        Assert.Equal(1_000 - 151, m.UnallocatedSp); // ceil(250 × 100 / 166) = 151
    }

    [Fact]
    public void Enqueueing_applies_unallocated_sp()
    {
        var m = Member();
        Training.Grant(m, 300, Data);
        Assert.Equal(300, m.UnallocatedSp);

        Assert.True(Training.Enqueue(m, "archery", Data, out var applied));

        Assert.Equal([new LevelUp("m", "archery", 1)], applied!.LevelUps);
        Assert.Equal(50, m.UnallocatedSp);
    }

    [Fact]
    public void Enqueue_requires_prerequisites_from_levels_or_earlier_queue()
    {
        var m = Member();
        Assert.False(Training.Enqueue(m, "rapid", Data, out _));
        Assert.Equal("archery", Assert.Single(Training.MissingPrerequisites(m, "rapid", Data)).SkillId);

        Enqueue(m, "archery");
        Enqueue(m, "archery");
        Assert.True(Training.Enqueue(m, "rapid", Data, out _));
        Assert.Equal(new TrainingQueueEntry("rapid", 1), m.TrainingQueue[^1]);
    }

    [Fact]
    public void Enqueue_stops_at_max_level()
    {
        var m = Member(sp: new Dictionary<string, int> { ["heavy"] = 45_255 }); // Lv4
        Enqueue(m, "heavy");

        Assert.Null(Training.NextQueueLevel(m, "heavy", Data));
        Assert.False(Training.Enqueue(m, "heavy", Data, out _));
    }

    [Fact]
    public void Removing_an_entry_drops_entries_that_depended_on_it()
    {
        var m = Member();
        Enqueue(m, "archery"); // 1
        Enqueue(m, "archery"); // 2
        Enqueue(m, "heavy");   // 1
        Enqueue(m, "rapid");   // 1 (archery 2 필요)

        var removed = Training.RemoveFromQueue(m, 0, Data);

        // archery 1을 지우면 archery 2, rapid 1도 순서가 맞지 않게 된다.
        Assert.Equal(3, removed);
        Assert.Equal([new TrainingQueueEntry("heavy", 1)], m.TrainingQueue);
    }

    [Fact]
    public void Moving_entries_respects_order_rules()
    {
        var m = Member();
        Enqueue(m, "archery"); // 1
        Enqueue(m, "archery"); // 2
        Enqueue(m, "heavy");   // 1

        Assert.False(Training.MoveInQueue(m, 1, -1, Data)); // archery 2를 archery 1 앞으로: 안 됨
        Assert.True(Training.MoveInQueue(m, 2, -1, Data));  // heavy를 한 칸 위로: 됨
        Assert.Equal(["archery", "heavy", "archery"], m.TrainingQueue.Select(e => e.SkillId));
    }

    [Theory]
    [InlineData(CombatOutcome.Victory, 1_000)]
    [InlineData(CombatOutcome.Draw, 500)]
    [InlineData(CombatOutcome.Defeat, 300)]
    public void Combat_rewards_depend_on_outcome(CombatOutcome outcome, int sp)
    {
        Assert.Equal(sp, TrainingRules.Default.RewardFor(outcome));
    }

    [Fact]
    public void Save_round_trips_queue_and_unallocated_sp_and_rejects_broken_queue()
    {
        var m = Member();
        Enqueue(m, "archery");
        Enqueue(m, "archery");
        Training.Grant(m, 400, Data);
        var party = new Party([m]);

        var loaded = SaveGame.Deserialize(SaveGame.Serialize(party), Data).Members[0];

        Assert.Equal(m.TrainingQueue, loaded.TrainingQueue);
        Assert.Equal(m.SkillPoints, loaded.SkillPoints);

        var broken = """
            { "version": 2, "party": [ { "id": "a", "name": "이름", "row": "Front",
              "stats": { "str": 1, "dex": 1, "vital": 1, "intel": 1, "speed": 1 },
              "trainingQueue": [ { "skillId": "archery", "level": 2 } ] } ] }
            """;
        var errors = Assert.Throws<SaveGameException>(() => SaveGame.Deserialize(broken, Data)).Errors;
        Assert.Contains("save member 'a': training queue is out of order or skips prerequisites", errors);
    }
}
