using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CharacterAvatar : EntityAvatar
{
    [SerializeField] Transform _weaponPoint;
    [SerializeField] Transform _weaponBackPoint;
    [SerializeField] Transform _weaponSidePoint;
    private List<Quant> _quants = new List<Quant>();

    public List<Quant> Quants { get { return _quants; } }

    public event Action StartApplainQuants;
    public event Action EndApplainQuants;

    private bool _quantsApplaying = false;

    public void TakeItem(ItemObject itemObject)
    {

            if (itemObject.Item is Weapon)
            {
                if ((itemObject.Item as Weapon).WeaponType == WeaponType.Rifle || (itemObject.Item as Weapon).WeaponType == WeaponType.AssaultRifle)
                    itemObject.gameObject.transform.parent = _weaponBackPoint;
                else
                    itemObject.gameObject.transform.parent = _weaponSidePoint;
                itemObject.gameObject.transform.localPosition = Vector3.zero;
                itemObject.gameObject.transform.localRotation = Quaternion.identity;
            }
            itemObject.Take();
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

    public void RemoveLastQuant()
    {
        if (_quants.Count > 0)
            _quants.RemoveAt(_quants.Count - 1);
    }

    public void RemoveAllQuants()
    {
        _quants.Clear();
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
        
        if ( _quantsApplaying)        
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
                        var itemObject = _quants[0].Object as ItemObject;
                        TakeItem(itemObject);
                        quantEnded = true;
                    }
                    break;
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

}
