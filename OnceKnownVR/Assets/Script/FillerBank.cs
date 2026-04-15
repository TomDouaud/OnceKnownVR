using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
//  FillerBank
//  Picks a random French filler phrase to let the visitor know
//  the guide is thinking / processing their question.
// ════════════════════════════════════════════════════════════════════════════

public static class FillerBank
{
    private static readonly List<string> Phrases = new List<string>
    {
        "Hmm, laissez-moi réfléchir un instant.",
        "Bonne question, je cherche dans ma mémoire.",
        "Un moment, je rassemble mes idées.",
        "Voyons voir, laissez-moi y penser.",
        "Ah, attendez, ça me revient.",
        "Je réfléchis à la meilleure façon de vous expliquer ça.",
        "Donnez-moi une seconde, je fouille dans mes connaissances.",
        "Hmm, c'est une question intéressante, je prépare ma réponse.",
    };

    private static int _lastIndex = -1;

    public static string Pick()
    {
        int index;
        do { index = Random.Range(0, Phrases.Count); }
        while (index == _lastIndex && Phrases.Count > 1);
        _lastIndex = index;
        return Phrases[index];
    }
}