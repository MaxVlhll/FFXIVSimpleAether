using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace SimpleAether;

/// <summary>
/// Traduit un territoire en cibles a epingler. Ne connait ni l'UI ni les marqueurs.
/// Les positions ne dependent pas de la progression : elles sont mises en cache par
/// territoire. Seul l'etat de deblocage est reevalue a chaque appel de Resolve.
/// </summary>
public sealed class CurrentResolver
{
    private readonly record struct Candidate(
        Vector3 WorldPos, TargetKind Kind, uint AetherCurrentId, uint QuestId, string Label);

    private readonly Dictionary<uint, List<Candidate>> cache = new();
    private readonly HashSet<uint> loggedMissing = new();

    // EObj.RowId -> Level(s), pour retrouver la position d'un objet du monde.
    // Un meme EObj peut etre place dans plusieurs territoires : filtrer par territoire a l'usage.
    private Dictionary<uint, List<Level>>? levelByObject;
    // AetherCurrent.RowId -> EObj.RowId.
    private Dictionary<uint, uint>? eObjByAetherCurrent;

    public void InvalidateCache()
    {
        cache.Clear();
        loggedMissing.Clear();
    }

    public IReadOnlyList<AetherTarget> Resolve(uint territoryId)
    {
        if (!cache.TryGetValue(territoryId, out var candidates))
        {
            candidates = BuildCandidates(territoryId);
            cache[territoryId] = candidates;
        }

        if (candidates.Count == 0) return [];

        var result = new List<AetherTarget>(candidates.Count);
        foreach (var c in candidates)
        {
            if (IsUnlocked(c.AetherCurrentId)) continue;
            if (c.Kind == TargetKind.Quest && !IsQuestWorthShowing(c.QuestId)) continue;
            result.Add(new AetherTarget(c.WorldPos, c.Kind, c.AetherCurrentId, c.Label));
        }
        return result;
    }

    private static unsafe bool IsUnlocked(uint aetherCurrentId)
    {
        var state = PlayerState.Instance();
        return state != null && state->IsAetherCurrentUnlocked(aetherCurrentId);
    }

    private static unsafe bool IsQuestWorthShowing(uint questId)
    {
        var quests = QuestManager.Instance();
        if (quests == null) return false;
        if (QuestManager.IsQuestComplete(questId)) return false;

        var questSheet = Plugin.DataManager.GetExcelSheet<Quest>();
        if (questSheet?.GetRowOrDefault(questId) is not { } quest) return false;

        // Une quete deja acceptee mais pas finie reste epinglee sur son emetteur.
        var done = new List<bool>();
        foreach (var prev in quest.PreviousQuest)
        {
            if (prev.RowId == 0) continue;
            done.Add(QuestManager.IsQuestComplete(prev.RowId));
        }
        return QuestPrereq.ArePrerequisitesMet(
            CollectionsMarshal.AsSpan(done), quest.PreviousQuestJoin);
    }

    private List<Candidate> BuildCandidates(uint territoryId)
    {
        var result = new List<Candidate>();

        var flgSets = Plugin.DataManager.GetExcelSheet<AetherCurrentCompFlgSet>();
        if (flgSets == null)
        {
            Plugin.Log.Warning("Sheet AetherCurrentCompFlgSet indisponible.");
            return result;
        }

        AetherCurrentCompFlgSet? set = null;
        foreach (var row in flgSets)
        {
            if (row.Territory.RowId == territoryId) { set = row; break; }
        }
        if (set is not { } flgSet) return result;   // zone sans vents : rien a faire

        EnsureLookups();

        foreach (var currentRef in flgSet.AetherCurrents)
        {
            if (currentRef.RowId == 0) continue;
            if (currentRef.ValueNullable is not { } current) continue;

            if (current.Quest.RowId != 0)
            {
                var questId = current.Quest.RowId;
                var quest = current.Quest.ValueNullable;
                var level = quest?.IssuerLocation.ValueNullable;
                if (level == null)
                {
                    WarnOnce(currentRef.RowId, $"vent de quete {currentRef.RowId} : IssuerLocation introuvable");
                    continue;
                }
                var label = quest?.Name.ExtractText() ?? "Quest";
                result.Add(new Candidate(
                    new Vector3(level.Value.X, level.Value.Y, level.Value.Z),
                    TargetKind.Quest, currentRef.RowId, questId, label));
            }
            else
            {
                Level? level = null;
                if (eObjByAetherCurrent!.TryGetValue(currentRef.RowId, out var eObjId)
                    && levelByObject!.TryGetValue(eObjId, out var levels))
                {
                    foreach (var l in levels)
                    {
                        if (l.Territory.RowId != territoryId) continue;
                        level = l;
                        break;
                    }
                }

                if (level is { } l2)
                {
                    result.Add(new Candidate(
                        new Vector3(l2.X, l2.Y, l2.Z),
                        TargetKind.Field, currentRef.RowId, 0, "Aether current"));
                }
                else
                {
                    WarnOnce(currentRef.RowId,
                        $"vent de terrain {currentRef.RowId} : jointure EObj/Level vide ou hors territoire {territoryId}");
                }
            }
        }

        Plugin.Log.Information(
            $"Territoire {territoryId} : {result.Count} vent(s) candidat(s) resolu(s).");
        return result;
    }

    private void EnsureLookups()
    {
        if (levelByObject != null && eObjByAetherCurrent != null) return;

        levelByObject = new Dictionary<uint, List<Level>>();
        eObjByAetherCurrent = new Dictionary<uint, uint>();

        var eObjSheet = Plugin.DataManager.GetExcelSheet<EObj>();
        if (eObjSheet != null)
        {
            foreach (var eObj in eObjSheet)
            {
                var data = eObj.Data.RowId;
                if (data != 0) eObjByAetherCurrent.TryAdd(data, eObj.RowId);
            }
        }
        else Plugin.Log.Warning("Sheet EObj indisponible.");

        // eObjByAetherCurrent est deja rempli ci-dessus : la sheet Level fait des
        // dizaines de milliers de lignes, mais seule une poignee de EObj nous interesse.
        var wanted = new HashSet<uint>(eObjByAetherCurrent.Values);

        var levelSheet = Plugin.DataManager.GetExcelSheet<Level>();
        if (levelSheet != null)
        {
            foreach (var level in levelSheet)
            {
                var obj = level.Object.RowId;
                if (obj == 0 || !wanted.Contains(obj)) continue;
                if (!levelByObject.TryGetValue(obj, out var list))
                    levelByObject[obj] = list = new List<Level>();
                list.Add(level);
            }
        }
        else Plugin.Log.Warning("Sheet Level indisponible.");
    }

    private void WarnOnce(uint aetherCurrentId, string message)
    {
        if (loggedMissing.Add(aetherCurrentId)) Plugin.Log.Warning(message);
    }
}
