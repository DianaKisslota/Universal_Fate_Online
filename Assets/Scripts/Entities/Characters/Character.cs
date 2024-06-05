using System.Collections.Generic;

public class Character : BaseEntity
{
    public string ClassName {  get; set; }
    private int _expirience;
    public int Expirience
    {
        get { return _expirience; }
        set { _expirience = value; }
    }

    public List<Skill> Skills { get; set; } = new List<Skill>();
    public Weapon MainWeapon { get; set; }
    public Weapon SecondaryWeapon { get; set; }
    public Weapon ShoulderWeapon { get; set; }

    public CharacterInventory Inventory { get; private set; }  = new CharacterInventory();
}
