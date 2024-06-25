﻿public class AK47 : RangeWeapon
{
    public AK47()
    {
        Name = "АК-47";
        WeaponType = WeaponType.AssaultRifle;
        Caliber = Caliber.bullet762x39;
        AmmoCapacity = 30;
        Weight = 3.3f;
        Volume = 10;
        AddSkill(SkillType.AssaultRifle, 1);

        ShortBurst = 3;
        LongBurst = 5;
    }
}

