using UnityEngine;

namespace PC.Component
{
    /// <summary>Latch the artist-authored lid and four caddies on a newly
    /// purchased chassis. Saves restore those independent items themselves;
    /// do not spawn another default set when loading the case prefab.</summary>
    public class ServerChassis : MonoBehaviour
    {
        [SerializeField] private Item factoryCover;
        [SerializeField] private Slot coverSlot;
        [SerializeField] private DriveTray[] factoryTrays;
        [SerializeField] private Slot[] trayBays;
        [SerializeField] private Item factoryDrive;
        [SerializeField] private HardwareSlot factoryDriveSlot;
        [System.Serializable]
        public class FactoryPart
        {
            public Item item;
            public Slot slot;
        }

        // All geometry is serialized in the ready-server prefab. This list
        // only restores slot state/joints on the already-present components.
        [SerializeField] private FactoryPart[] factoryParts;
        private bool loading;

        // Сервер — это полтора десятка Rigidbody, скреплённых FixedJoint'ами:
        // корпус, крышка, четыре корзины, плата с процессором, кулером и
        // памятью, БП и накопители. При штатных итерациях решателя (6/1)
        // такие джойнты «мягкие»: стоит потянуть сборку пружиной переноса, и
        // детали отстают от корпуса — до 0.32 м/с и 1 см. В игре это и есть
        // дрожь собранного сервера, сильнее всего заметная по плате с её
        // обвесом и по корзинам HDD.
        //
        // Итерации решателя ничего не утяжеляют и физику не отключают: они
        // лишь точнее считают уже существующие джойнты — тот же приём, что
        // в ReadyBuildSpawner.StabilizeHost и CableItem (12/8 на кабель).
        // На топовой сборке (30 тел) 6/1 даёт 0.27 м/с дрожи и 12 мм
        // отставания, 12/8 — 0.04 м/с и 1.3 мм; 20/10 в замерах ничего не
        // добавляет (только жжёт процессор), поэтому остаёмся на 12/8.
        public const int AssemblySolverIterations = 12;
        public const int AssemblySolverVelocityIterations = 8;

        private void Awake()
        {
            Stabilize(GetComponent<Rigidbody>());
            loading = SaveManager.RestoringSave;
            if (!loading) return;
            if (factoryParts != null)
                for (int i = factoryParts.Length - 1; i >= 0; i--)
                    if (factoryParts[i] != null) RemoveFactoryPart(factoryParts[i].item);
            RemoveFactoryPart(factoryDrive);
            RemoveFactoryPart(factoryCover);
            if (factoryTrays != null)
                foreach (var tray in factoryTrays) RemoveFactoryPart(tray);
        }

        private static void RemoveFactoryPart(Item part)
        {
            if (part == null) return;
            part.gameObject.SetActive(false);
            part.transform.SetParent(null, true);
            Destroy(part.gameObject);
        }

        private void Start()
        {
            if (!loading) AttachFactoryParts();

            // Фабричные детали к этому моменту уже в слотах; те, что игрок
            // поставит позже, стабилизирует сам слот (см. Slot.SetComponent).
            var bodies = GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++) Stabilize(bodies[i]);
        }

        private void AttachFactoryParts()
        {
            if (factoryCover != null && coverSlot != null) coverSlot.TryAttach(factoryCover);
            if (factoryTrays != null && trayBays != null)
                for (int i = 0; i < Mathf.Min(factoryTrays.Length, trayBays.Length); i++)
                    if (factoryTrays[i] != null && trayBays[i] != null)
                        trayBays[i].TryAttach(factoryTrays[i]);
            if (factoryParts != null)
                foreach (var part in factoryParts)
                    if (part != null && part.item != null && part.slot != null && !part.slot.IsUsing)
                        part.slot.TryAttach(part.item);
            if (factoryDrive != null && factoryDriveSlot != null)
                factoryDriveSlot.TryAttach(factoryDrive);
        }

        /// <summary>
        /// Поднять телу итерации решателя до серверных. Меньшие значения не
        /// понижаем: у деталей могут быть свои, более высокие настройки.
        /// </summary>
        public static void Stabilize(Rigidbody body)
        {
            if (body == null) return;
            if (body.solverIterations < AssemblySolverIterations)
                body.solverIterations = AssemblySolverIterations;
            if (body.solverVelocityIterations < AssemblySolverVelocityIterations)
                body.solverVelocityIterations = AssemblySolverVelocityIterations;
        }

        /// <summary>
        /// Слот только что посадил деталь на тело host. Если это серверная
        /// сборка — считаем деталь так же точно, иначе свежая плата или
        /// накопитель снова начнут отставать от корпуса.
        /// </summary>
        public static void StabilizeIfServerPart(Rigidbody host, Rigidbody part)
        {
            if (part == null) return;
            if (!InServerAssembly(host) && !InServerAssembly(part)) return;

            Stabilize(host);
            Stabilize(part);
        }

        /// <summary>
        /// Принадлежит ли тело серверу: либо оно в иерархии корпуса, либо
        /// висит на нём через цепочку FixedJoint'ов. Второй путь нужен потому,
        /// что слоты с setParent: 0 (плата) не делают деталь потомком корпуса —
        /// связь остаётся только в графе джойнтов.
        /// </summary>
        private static bool InServerAssembly(Rigidbody body)
        {
            if (body == null) return false;
            if (body.GetComponent<ServerChassis>() != null) return true;
            if (body.GetComponentInParent<ServerChassis>() != null) return true;

            var current = body;
            for (int i = 0; i < 8 && current != null; i++)
            {
                var joint = current.GetComponent<FixedJoint>();
                if (joint == null || joint.connectedBody == null) return false;

                current = joint.connectedBody;
                if (current.GetComponent<ServerChassis>() != null) return true;
            }

            return false;
        }
    }
}
