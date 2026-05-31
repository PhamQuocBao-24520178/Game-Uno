using System;

[Serializable]
public class UnoCardData
{
    public UnoColor color;
    public UnoCardType type;
    public int number;

    public UnoCardData(UnoColor color, UnoCardType type, int number = -1)
    {
        this.color = color;
        this.type = type;
        this.number = number;
    }

    public string GetDisplayText()
    {
        switch (type)
        {
            case UnoCardType.Number:
                return number.ToString();

            case UnoCardType.Skip:
                return "SKIP";

            case UnoCardType.Reverse:
                return "REV";

            case UnoCardType.DrawTwo:
                return "+2";

            case UnoCardType.Wild:
                return "WILD";

            case UnoCardType.WildDrawFour:
                return "+4";

            default:
                return "?";
        }
    }
}

public enum UnoColor
{
    Red,
    Yellow,
    Green,
    Blue,
    Wild
}

public enum UnoCardType
{
    Number,
    Skip,
    Reverse,
    DrawTwo,
    Wild,
    WildDrawFour
}