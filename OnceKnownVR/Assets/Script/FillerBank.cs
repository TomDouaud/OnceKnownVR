using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
//  FillerBank
//  Picks a random French filler phrase to play while LLM processes.
//  Each phrase ends mid-thought so the LLM continuation sounds seamless.
// ════════════════════════════════════════════════════════════════════════════

public static class FillerBank
{
    private static readonly List<string> PhrasesOllama = new List<string>
    {
        "Ah oui, je vois tout à fait de quoi vous parlez, c'est une question que beaucoup de visiteurs me posent et qui me tient vraiment à cœur, car ce sujet est au centre même de notre collection, il s'agit de",
        "Très bonne question, en effet c'est quelque chose que j'ai étudié en détail pendant de nombreuses années, et chaque fois que j'y pense je trouve de nouveaux aspects fascinants, cela concerne directement",
        "Intéressant, vous avez remarqué quelque chose que peu de gens perçoivent au premier regard, il faut vraiment prendre le temps d'observer attentivement pour comprendre toute la profondeur de cette œuvre, et ce que vous voyez là c'est",
        "Oui absolument, permettez-moi de vous expliquer cela en détail car c'est un sujet qui mérite vraiment qu'on s'y attarde, il y a toute une histoire derrière ce que vous voyez ici et elle est particulièrement remarquable,",
        "Bien sûr, c'est quelque chose de fascinant que peu de musées au monde peuvent se vanter de posséder, nous avons eu la chance extraordinaire d'acquérir cette pièce et je suis toujours ému quand j'en parle,",
        "Je suis vraiment content que vous demandiez cela car c'est précisément ce genre de curiosité qui fait de vous un visiteur exceptionnel, la plupart des gens passent devant sans s'interroger, mais vous avez remarqué quelque chose d'important,",
        "Tout à fait, et c'est justement ce qui rend cette pièce absolument unique dans toute notre collection, il n'existe pratiquement aucun autre exemple comparable dans le monde entier, et l'histoire de sa création est elle-même extraordinaire,",
        "Vous savez, c'est une excellente observation et elle me rappelle une anecdote que j'aime partager avec les visiteurs les plus attentifs, car derrière chaque œuvre se cache une histoire humaine profonde et touchante, parce que",
    };
    
    private static readonly List<string> Phrases = new List<string>
    {
        "Ah oui, je vois tout à fait de quoi vous parlez.",
        "Très bonne question, permettez-moi de vous expliquer.",
        "Intéressant, vous avez remarqué quelque chose d'important.",
        "Oui absolument, c'est un sujet qui me tient à cœur.",
        "Bien sûr, c'est quelque chose de fascinant.",
        "Je suis content que vous demandiez cela.",
        "Tout à fait, c'est ce qui rend cette pièce unique.",
        "Vous savez, c'est une excellente observation.",
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