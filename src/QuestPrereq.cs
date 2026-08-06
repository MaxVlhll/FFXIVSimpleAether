using System;

namespace SimpleAether;

public static class QuestPrereq
{
    /// <summary>
    /// Quest.PreviousQuestJoin vaut 0 quand toutes les quetes prealables listees
    /// doivent etre completes, et autre chose quand une seule suffit.
    /// L'appelant a deja retire les RowRef vides (RowId == 0) de la liste.
    /// </summary>
    public static bool ArePrerequisitesMet(ReadOnlySpan<bool> previousComplete, byte join)
    {
        if (previousComplete.Length == 0) return true;

        if (join == 0)
        {
            foreach (var done in previousComplete)
                if (!done) return false;
            return true;
        }

        foreach (var done in previousComplete)
            if (done) return true;
        return false;
    }
}
