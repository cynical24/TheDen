using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.NPC.Systems;
using Content.Server.Objectives;
using Content.Server.PDA.Ringer;
using Content.Server.Roles;
using Content.Server.Traitor.Uplink;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mood;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using System.Text;

namespace Content.Server.GameTicking.Rules;

public sealed class TraitorRuleSystem : GameRuleSystem<TraitorRuleComponent>
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly UplinkSystem _uplink = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitorRuleComponent, AfterAntagEntitySelectedEvent>(AfterEntitySelected);

        SubscribeLocalEvent<TraitorRuleComponent, ObjectivesTextPrependEvent>(OnObjectivesTextPrepend);
    }

    protected override void Added(EntityUid uid, TraitorRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);
        MakeCodewords(component);
    }

    private void AfterEntitySelected(Entity<TraitorRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeTraitor(args.EntityUid, ent);
    }

    private void MakeCodewords(TraitorRuleComponent component)
    {
        var adjectives = _prototypeManager.Index(component.CodewordAdjectives).Values;
        var verbs = _prototypeManager.Index(component.CodewordVerbs).Values;
        var codewordPool = adjectives.Concat(verbs).ToList();
        var finalCodewordCount = Math.Min(component.CodewordCount, codewordPool.Count);
        component.Codewords = new string[finalCodewordCount];
        for (var i = 0; i < finalCodewordCount; i++)
        {
            component.Codewords[i] = _random.PickAndTake(codewordPool);
        }
    }

    public bool MakeTraitor(EntityUid traitor, TraitorRuleComponent component)
    {
        //Grab the mind if it wasnt provided
        if (!_mindSystem.TryGetMind(traitor, out var mindId, out var mind))
            return false;

        var briefing = "";

        if (component.GiveCodewords)
            briefing = Loc.GetString("traitor-role-codewords-short", ("codewords", string.Join(", ", component.Codewords)));

        var issuer = _random.Pick(_prototypeManager.Index(component.ObjectiveIssuers).Values);

        Note[]? code = null;

        if (component.GiveUplink)
        {
            // Calculate the amount of currency on the uplink.
            var startingBalance = component.StartingBalance;
            if (_jobs.MindTryGetJob(mindId, out _, out var prototype))
            {
                if (startingBalance < job.AntagAdvantage) // Can't use Math functions on FixedPoint2
                    startingBalance = 0;
                else
                    startingBalance = startingBalance - job.AntagAdvantage;
            }

            // creadth: we need to create uplink for the antag.
            // PDA should be in place already
            var pda = _uplink.FindUplinkTarget(traitor);
            if (pda == null || !_uplink.AddUplink(traitor, startingBalance, giveDiscounts: true))
                return false;

            // Give traitors their codewords and uplink code to keep in their character info menu
            code = EnsureComp<RingerUplinkComponent>(pda.Value).Code;

            // If giveUplink is false the uplink code part is omitted
            briefing = string.Format("{0}\n{1}", briefing,
                Loc.GetString("traitor-role-uplink-code-short", ("code", string.Join("-", code).Replace("sharp", "#"))));
        }

        _antag.SendBriefing(traitor, GenerateBriefing(component.Codewords, code, issuer), null, component.GreetSoundNotification);


        component.TraitorMinds.Add(mindId);

        // Assign briefing
        _roleSystem.MindAddRole(mindId, new RoleBriefingComponent
        {
            Briefing = briefing
        }, mind, true);

        // Don't Change the faction, this was stupid.
        //_npcFaction.RemoveFaction(traitor, component.NanoTrasenFaction, false);
        //_npcFaction.AddFaction(traitor, component.SyndicateFaction);

        RaiseLocalEvent(traitor, new MoodEffectEvent("TraitorFocused"));

        return true;
    }

    private (Note[]?, string) RequestUplink(EntityUid traitor, FixedPoint2 startingBalance, string briefing)
    {
        var pda = _uplink.FindUplinkTarget(traitor);
        Note[]? code = null;

        var uplinked = _uplink.AddUplink(traitor, startingBalance, pda, true);

        if (pda is not null && uplinked)
        {
            // Codes are only generated if the uplink is a PDA
            code = EnsureComp<RingerUplinkComponent>(pda.Value).Code;

            // If giveUplink is false the uplink code part is omitted
            briefing = string.Format("{0}\n{1}",
                briefing,
                Loc.GetString("traitor-role-uplink-code-short", ("code", string.Join("-", code).Replace("sharp", "#"))));
            return (code, briefing);
        }
        else if (pda is null && uplinked)
        {
            briefing += "\n" + Loc.GetString("traitor-role-uplink-implant-short");
        }

        return (null, briefing);
    }

    // TODO: AntagCodewordsComponent
    private void OnObjectivesTextPrepend(EntityUid uid, TraitorRuleComponent comp, ref ObjectivesTextPrependEvent args)
    {
        args.Text += "\n" + Loc.GetString("traitor-round-end-codewords", ("codewords", string.Join(", ", comp.Codewords)));
    }

    // TODO: figure out how to handle this? add priority to briefing event?
    private string GenerateBriefing(string[]? codewords, Note[]? uplinkCode, string? objectiveIssuer = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("traitor-role-greeting", ("corporation", objectiveIssuer ?? Loc.GetString("objective-issuer-unknown"))));
        if (codewords != null)
            sb.AppendLine(Loc.GetString("traitor-role-codewords", ("codewords", string.Join(", ", codewords))));
        if (uplinkCode != null)
            sb.AppendLine(Loc.GetString("traitor-role-uplink-code", ("code", string.Join("-", uplinkCode).Replace("sharp", "#"))));
        else
            sb.AppendLine(Loc.GetString("traitor-role-uplink-implant"));


        return sb.ToString();
    }

    public List<(EntityUid Id, MindComponent Mind)> GetOtherTraitorMindsAliveAndConnected(MindComponent ourMind)
    {
        List<(EntityUid Id, MindComponent Mind)> allTraitors = new();

        var query = EntityQueryEnumerator<TraitorRuleComponent>();
        while (query.MoveNext(out var uid, out var traitor))
        {
            foreach (var role in GetOtherTraitorMindsAliveAndConnected(ourMind, (uid, traitor)))
            {
                if (!allTraitors.Contains(role))
                    allTraitors.Add(role);
            }
        }

        return allTraitors;
    }

    private List<(EntityUid Id, MindComponent Mind)> GetOtherTraitorMindsAliveAndConnected(MindComponent ourMind, Entity<TraitorRuleComponent> rule)
    {
        var traitors = new List<(EntityUid Id, MindComponent Mind)>();
        foreach (var mind in _antag.GetAntagMinds(rule.Owner))
        {
            if (mind.Comp == ourMind)
                continue;

            traitors.Add((mind, mind));
        }

        return traitors;
    }
}
