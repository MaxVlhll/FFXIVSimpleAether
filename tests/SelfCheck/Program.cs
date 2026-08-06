using System;
using SimpleAether;

var failures = 0;

void Check(string name, bool ok)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
    if (!ok) failures++;
}

Check("liste vide => prerequis remplis",
    QuestPrereq.ArePrerequisitesMet([], 0));
Check("ET, toutes completes => remplis",
    QuestPrereq.ArePrerequisitesMet([true, true, true], 0));
Check("ET, une manquante => non remplis",
    !QuestPrereq.ArePrerequisitesMet([true, false, true], 0));
Check("ET, une seule complete => remplis",
    QuestPrereq.ArePrerequisitesMet([true], 0));
Check("OU, une complete => remplis",
    QuestPrereq.ArePrerequisitesMet([false, true], 1));
Check("OU, aucune complete => non remplis",
    !QuestPrereq.ArePrerequisitesMet([false, false], 1));
Check("OU, liste vide => remplis",
    QuestPrereq.ArePrerequisitesMet([], 1));

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
