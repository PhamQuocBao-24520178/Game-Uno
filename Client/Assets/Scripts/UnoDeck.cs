using System.Collections.Generic;
using UnityEngine;

public class UnoDeck
{
    private readonly List<UnoCardData> cards = new List<UnoCardData>();

    public int Count
    {
        get { return cards.Count; }
    }

    public UnoDeck()
    {
        CreateDeck();
        Shuffle();
    }

    private void CreateDeck()
    {
        cards.Clear();

        AddColorCards(UnoColor.Red);
        AddColorCards(UnoColor.Yellow);
        AddColorCards(UnoColor.Green);
        AddColorCards(UnoColor.Blue);

        for (int i = 0; i < 4; i++)
        {
            cards.Add(new UnoCardData(UnoColor.Wild, UnoCardType.Wild));
            cards.Add(new UnoCardData(UnoColor.Wild, UnoCardType.WildDrawFour));
        }
    }

    private void AddColorCards(UnoColor color)
    {
        cards.Add(new UnoCardData(color, UnoCardType.Number, 0));

        for (int number = 1; number <= 9; number++)
        {
            cards.Add(new UnoCardData(color, UnoCardType.Number, number));
            cards.Add(new UnoCardData(color, UnoCardType.Number, number));
        }

        for (int i = 0; i < 2; i++)
        {
            cards.Add(new UnoCardData(color, UnoCardType.Skip));
            cards.Add(new UnoCardData(color, UnoCardType.Reverse));
            cards.Add(new UnoCardData(color, UnoCardType.DrawTwo));
        }
    }

    public void Shuffle()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int randomIndex = Random.Range(i, cards.Count);

            UnoCardData temp = cards[i];
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }

    public UnoCardData DrawCard()
    {
        if (cards.Count == 0)
        {
            Debug.LogWarning("Deck is empty!");
            return null;
        }

        UnoCardData topCard = cards[0];
        cards.RemoveAt(0);
        return topCard;
    }
}