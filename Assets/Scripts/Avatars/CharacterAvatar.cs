using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class CharacterAvatar : EntityAvatar
{
    [SerializeField] private Transform _weaponPoint;
    [SerializeField] private Transform _weaponBackPoint;
    [SerializeField] private Transform _weaponSidePoint;
    public CharacterInventoryPresenter InventoryPresenter { get; set; }

    private List<Quant> _quants = new List<Quant>();
    public Character Character => Entity as Character;

    private Dictionary<Item, ItemObject> _itemObjects = new Dictionary<Item, ItemObject>();
    public List<Quant> Quants { get { return _quants; } }

    public event Action StartApplainQuants;
    public event Action EndApplainQuants;
    public event Action<FireMode> FireModeSet;
    public event Action<DropSlot, DropSlot, ItemPresenter> ItemPresenterTransferred;

    private bool _quantsApplaying = false;

    private float _isFiring = 0f;
    private float _isReloading = 0f;

    public FireMode FireMode { get; set; }

    protected override AudioClip _rangeAttackSound
    {
        get
        {
            if (Character.MainWeapon != null)
                return Global.GetSoundFor(Character.MainWeapon.GetType(), SoundType.Shot);
            else
                return null;
        }
    }

    protected override void Init()
    {
        Character.OnEquip += OnEquip;
        Character.OnUnEquip += OnUnEquip;
    }

    private void OnDestroy()
    {
        Character.OnEquip -= OnEquip;
        Character.OnUnEquip -= OnUnEquip;
    }

    private ItemObject GetItemObject(Item item)
    {
        ItemObject resultObject = null;
        if (_itemObjects.ContainsKey(item))
        {
            resultObject = _itemObjects[item];
        }
        else
        {
            resultObject = ItemFactory.CreateItem(item);
            _itemObjects.Add(item, resultObject);
        }

        return resultObject;
    }
    private void OnEquip(Item item, SlotType slotType)
    {
        ReflectEquipment(item, slotType);

        InventoryPresenter.InitItemSlots();
    }

    public void ReflectAllItems()
    {
        ReflectEquipment(Character.MainWeapon, SlotType.MainWeapon);
        ReflectEquipment(Character.SecondaryWeapon, SlotType.SecondaryWeapon);
        ReflectEquipment(Character.ShoulderWeapon, SlotType.Shoulder);
    }

    public void ReflectEquipment(Item item, SlotType slotType)
    {
        if (item == null)
            return;
        var itemObject = GetItemObject(item);
        Quaternion rotation = Quaternion.identity;
        Vector3 position = Vector3.zero;
        if (slotType == SlotType.MainWeapon)
        {
            if (item is RangeWeapon rangeWeapon)
            {
                FireMode = rangeWeapon.SingleShot != null ? FireMode.SingleShot :
                                            rangeWeapon.ShortBurst != null ? FireMode.ShortBurst :
                                            rangeWeapon.LongBurst != null ? FireMode.LongBurst :
                                            0;
            }
            else
                FireMode = FireMode.Undefined;
            FireModeSet?.Invoke(FireMode);
        }

        switch (slotType)
        {
            case SlotType.MainWeapon:
                {
                    //_animator.ResetTrigger("Idle");
                    itemObject.gameObject.transform.parent = _weaponPoint;
                    switch ((item as Weapon).WeaponType)
                    {
                        case WeaponType.Rifle:
                        case WeaponType.AssaultRifle:
                        case WeaponType.SMG:
                            Animator.SetInteger("HasWeapon", 2);
                            break;
                        case WeaponType.Pistol:
                            Animator.SetInteger("HasWeapon", 1);
                            break;
                        case WeaponType.Knife:
                            {
                                Animator.SetInteger("HasWeapon", 3);
                                rotation = Quaternion.Euler(new Vector3(0, 0, -90));
                            }
                            break;
                    }
                    break;
                }
            case SlotType.SecondaryWeapon:
                {
                    if ((item as Weapon).WeaponType == WeaponType.Knife)
                    {
                        rotation = Quaternion.Euler(new Vector3(90, 90, 0));
                        position = new Vector3(0, -0.1f, -0.25f);
                    }
                    itemObject.gameObject.transform.parent = _weaponSidePoint;
                }
                break;
            case SlotType.Shoulder:
                itemObject.gameObject.transform.parent = _weaponBackPoint;
                break;
            default: return;
        }
        itemObject.gameObject.transform.localPosition = position;
        itemObject.gameObject.transform.localRotation = rotation;
        itemObject.gameObject.SetActive(true);
        itemObject.Take();
    }

    private void OnUnEquip(Item item, SlotType slotType)
    {
        var itemObject = GetItemObject(item);
        if (itemObject != null)
            itemObject.gameObject.SetActive(false);
        if (slotType == SlotType.MainWeapon)
        {
            Animator.SetInteger("HasWeapon", 0);
        }
    }

    public void TakeItem(ItemObject itemObject)
    {
        if (!_itemObjects.ContainsKey(itemObject.Item))
            _itemObjects.Add(itemObject.Item, itemObject);
        if (itemObject.Item is Weapon weapon)
        {
            if (Character.MainWeapon == null)
            {
                Character.Equip(weapon, SlotType.MainWeapon);
            }
            else
            {
                if (weapon.WeaponType == WeaponType.Rifle || weapon.WeaponType == WeaponType.AssaultRifle)
                {
                    if (Character.ShoulderWeapon == null)
                    {
                        Character.Equip(weapon, SlotType.Shoulder);
                    }
                    else
                    {
                        Character.Inventory.AddItem(weapon);
                        itemObject.Take();
                        itemObject.gameObject.SetActive(false);
                    }
                }
                else
                {
                    if (Character.SecondaryWeapon == null)
                    {
                        Character.Equip(weapon, SlotType.SecondaryWeapon);
                    }
                    else
                    {
                        Character.Inventory.AddItem(weapon);
                        itemObject.Take();
                        itemObject.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
    public void AddQuant(EntityAction action, object _object, Vector3? lastPosition, Quaternion lastRotation)
    {
        _quants.Add(new Quant(action, _object, lastPosition, lastRotation));
    }

    public void AddMoveQuant(Vector3 point)
    {
        AddQuant(EntityAction.Move, point, transform.position, transform.rotation);
    }

    public void AddPickObjectQuant(ItemObject itemObject)
    {
        AddQuant(EntityAction.PickObject, itemObject, itemObject.transform.position, itemObject.transform.rotation);
    }

    public void AddItemtransferQuant(TransferItemInfo transferItemInfo)
    {
        AddQuant(EntityAction.TransferItem, transferItemInfo, transform.position, transform.rotation);
    }

    public void AddReloadWeaponQuant(ReloadWeaponInfo reloadWeaponInfo)
    {
        AddQuant(EntityAction.ReloadWeapon, reloadWeaponInfo, transform.position, transform.rotation);
    }

    public void AddAttackQuant(AttackInfo attackInfo)
    {
        AddQuant(EntityAction.Attack, attackInfo, transform.position, transform.rotation);
    }

    public void RemoveLastQuant()
    {
        if (_quants.Count > 0)
            _quants.RemoveAt(_quants.Count - 1);
    }

    public void RemoveAllQuants()
    {
        _quants.Clear();
    }

    public void LookForShoot(EntityAvatar avatar)
    {
        var type = Character.MainWeapon.WeaponType;
        transform.LookAt(avatar.transform);
        if (type == WeaponType.SMG || type == WeaponType.Rifle || type == WeaponType.AssaultRifle || type == WeaponType.MG)
            transform.Rotate(0, 55, 0);
    }

    private void StartCurrentQuant()
    {
        if (_quants.Count == 0)
            return;
        switch (_quants[0].Action)
        {
            case EntityAction.Move:
                MoveTo((_quants[0].Object as Vector3?).Value);
                break;
            case EntityAction.PickObject:
                TakeItem(_quants[0].Object as ItemObject);
                break;
            case EntityAction.TransferItem:
                {
                    var transferItemInfo = _quants[0].Object as TransferItemInfo;
                    var sourceSlot = transferItemInfo.Source;
                    var destinationSlot = transferItemInfo.Destination;
                    var item = transferItemInfo.ItemPresenter;

                    TransferItem(sourceSlot, destinationSlot, item);

                }
                break;
            case EntityAction.ReloadWeapon:
                {
                    var reloadWeaponInfo = _quants[0].Object as ReloadWeaponInfo;
                    var sourceSlot = reloadWeaponInfo.SourceSlot;
                    var ammoPresenter = reloadWeaponInfo.AmmoPresenter;
                    var ammoUsed = reloadWeaponInfo.AmmoUsed;
                    var weaponPresenter = reloadWeaponInfo.WeaponPresenter;
                    var weapon = weaponPresenter.Item as RangeWeapon;
                    weapon.Reload(ammoPresenter.Item as Ammo, ammoUsed);
                    weaponPresenter.RefreshInfo();
                    ammoPresenter.Count -= ammoUsed;
                    sourceSlot.FillSlots();
                    if (weapon.WeaponType != WeaponType.Pistol)
                        PlaySound(Global.GetSoundFor(typeof(AK47), SoundType.Reload));
                    _isReloading = 0.5f;
                    break;
                }
            case EntityAction.Attack:
                {
                    var attackInfo = _quants[0].Object as AttackInfo;
                    var target = attackInfo.Target;
                    if (Character.MainWeapon is RangeWeapon rangeWeapon)
                    {
                        LookForShoot(target);
                        var rangeAttackData = new RangeAttackData();
                        rangeAttackData.Target = target.Entity;
                        rangeAttackData.AmmoType = attackInfo.AmmoType;
                        rangeAttackData.ShotNumber = rangeWeapon.GetFireModeAmmo(attackInfo.FireMode).Value;
                        rangeAttackData.PossibleShotNumber = Mathf.Min(rangeAttackData.ShotNumber, rangeWeapon.AmmoCount);
                        rangeAttackData.WeaponType = rangeWeapon.GetType();

                        rangeWeapon.UnLoad(rangeAttackData.PossibleShotNumber);

                        InventoryPresenter.RefreshItemSlots();

                        //var soundType = SoundType.Shot;
                        //if (FireMode == FireMode.ShortBurst)
                        //    soundType = SoundType.Burst;
                        //if (FireMode == FireMode.LongBurst)
                        //    soundType = SoundType.LongBurst;


                        RangeAttack(rangeAttackData);
                    }
                    break;
                }

            default:
                Debug.LogError("����������� ��� ��������");
                break;
        }
    }

    public void SetToPosition(Vector3 position)
    {
        _agent.destination = position;
        _agent.enabled = false;
        transform.position = position;
        _walkingTo = null;
        _agent.enabled = true;
    }

    public void ApplyQuants()
    {
        if (_quants.Count == 0)
            return;
        StartApplainQuants?.Invoke();
        _quantsApplaying = true;
        StartCurrentQuant();
    }

    protected override void CheckWalking()
    {
        base.CheckWalking();
    }

    protected override void AdditionChecks()
    {
        base.AdditionChecks();

        if (_quantsApplaying)
        {
            var quantEnded = false;
            switch (_quants[0].Action)
            {
                case EntityAction.Move:
                    {
                        quantEnded = _walkingTo == null;
                    }
                    break;
                case EntityAction.PickObject:
                    {
                        quantEnded = true;
                        break;
                    }
                case EntityAction.TransferItem:
                    {
                        quantEnded = true;
                        break;
                    }
                case EntityAction.ReloadWeapon:
                    {
                        _isReloading -= Time.deltaTime;
                        quantEnded = _isReloading <= 0;
                        break;
                    }
                case EntityAction.Attack:
                    {
                        _isFiring -= Time.deltaTime;
                        quantEnded = _isFiring <= 0;
                        if (quantEnded)
                            Animator.SetTrigger("Idle");
                        break;
                    }
                default:
                    Debug.LogError("����������� ��� ��������");
                    break;
            }
            if (quantEnded)
            {
                _quants.RemoveAt(0);
                if (_quants.Count > 0)
                    StartCurrentQuant();
                else
                {
                    _quantsApplaying = false;
                    EndApplainQuants?.Invoke();
                }
            }
        }
    }

    public void TransferItem(DropSlot sourceSlot, DropSlot destinationSlot, ItemPresenter itemPresenter)
    {
        itemPresenter.SetToParent(destinationSlot.transform);
        if (sourceSlot is CharacterItemSlot sourceItemSlot)
        {
            sourceSlot.OnItemLeave(itemPresenter.Item);
            sourceItemSlot.Character.UnEquip(itemPresenter.Item);
        }
        if (destinationSlot is CharacterItemSlot destinationItemSlot)
        {
            destinationItemSlot.PresenterSet(itemPresenter);
            destinationItemSlot.Character.Equip(itemPresenter.Item, destinationItemSlot);
        }
        if (sourceSlot is StorageSlot sourceStorageSlotSlot)
        {
            sourceStorageSlotSlot.Storage.RemoveItem(itemPresenter.Item);
        }
        if (destinationSlot is StorageSlot destinationStorageSlot)
        {
            destinationStorageSlot.Storage.AddItem(itemPresenter.Item);

        }
        ItemPresenterTransferred?.Invoke(sourceSlot, destinationSlot, itemPresenter);
        // destinationSlot.OnItemSet(itemPresenter.Item, destinationSlot);
    }

    protected override void RangeAttack(RangeAttackData attackData)
    {
        Animator.SetTrigger("Shoot");
        _isFiring = attackData.PossibleShotNumber / 4f;
        StopAgent(1f * attackData.PossibleShotNumber);

        var soundFire = Global.GetSoundFor(attackData.WeaponType, SoundType.Shot);
        var soundFailFire = Global.GetSoundFor(attackData.WeaponType, SoundType.FailShot);

        if (soundFire != null)
            PlaySound(soundFire, 0.1f, attackData.PossibleShotNumber);

        if (attackData.PossibleShotNumber == 0 && soundFailFire != null)
            PlaySound(soundFailFire);

        base.RangeAttack(attackData);
    }
}
