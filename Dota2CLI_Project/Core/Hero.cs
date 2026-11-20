namespace Dota2CLI.Core;

public sealed class Hero
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Attribute { get; set; } = "Unknown";
    public string AttackType { get; set; } = "Unknown";
    public int MaxHp { get; set; } = 100;
    public int Attack { get; set; } = 15;
    public int Armor { get; set; } = 5;
    public double Resist { get; set; } = 0.10;
    public int Speed { get; set; } = 100;
    public string ArtworkUrl { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsUserCreated { get; set; }

    public Hero Copy() => new()
    {
        Id = Id, Name = Name, Attribute = Attribute, AttackType = AttackType,
        MaxHp = MaxHp, Attack = Attack, Armor = Armor, Resist = Resist,
        Speed = Speed, ArtworkUrl = ArtworkUrl, Description = Description,
        IsUserCreated = IsUserCreated
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id)) throw new ArgumentException("ID персонажа не може бути порожнім.");
        if (string.IsNullOrWhiteSpace(Name)) throw new ArgumentException("Ім'я персонажа не може бути порожнім.");
        if (string.IsNullOrWhiteSpace(Attribute)) throw new ArgumentException("Атрибут персонажа не може бути порожнім.");
        if (string.IsNullOrWhiteSpace(AttackType)) throw new ArgumentException("Тип атаки не може бути порожнім.");
        if (MaxHp <= 0) throw new ArgumentOutOfRangeException(nameof(MaxHp), "MaxHp має бути більшим за 0.");
        if (Attack < 0 || Armor < 0) throw new ArgumentException("Атака та броня не можуть бути від'ємними.");
        if (double.IsNaN(Resist) || double.IsInfinity(Resist) || Resist < 0 || Resist > 1)
            throw new ArgumentOutOfRangeException(nameof(Resist), "Resist має бути в діапазоні від 0 до 1.");
        if (Speed <= 0) throw new ArgumentOutOfRangeException(nameof(Speed), "Speed має бути більшим за 0.");
    }
}

public sealed record HeroCardData(
    string Id, string Name, string Attribute, string AttackType,
    int MaxHp, int Attack, int Armor, double Resist, int Speed,
    string ArtworkUrl, string Description);
