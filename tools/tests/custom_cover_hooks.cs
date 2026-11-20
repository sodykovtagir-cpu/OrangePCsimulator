using System;
using System.Reflection;
using UnityEngine;

internal static class BoxTestHooks
{
    private static readonly FieldInfo PrefabField = typeof(Box).GetField("prefab",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Содержимое обычной (не кастомной) коробки задаётся в инспекторе.</summary>
    public static void SetPrefab(Box box, GameObject prefab) { PrefabField.SetValue(box, prefab); }

    /// <summary>Start() приватный — в игре его вызывает Unity на первом кадре.</summary>
    public static void Start(Box box) { typeof(Box).GetMethod("Start",
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(box, null); }

    /// <summary>Содержимое уже выложено ровно один раз.</summary>
    public static bool OpenedOnce(Box box)
    {
        return (bool)typeof(Box).GetField("opened",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(box);
    }

    /// <summary>Опустела ли очередь кастомных заказов (нет отложенного содержимого).</summary>
    public static bool CustomDeliveryWithoutPending()
    {
        return Box.CustomDelivery == null;
    }
}
