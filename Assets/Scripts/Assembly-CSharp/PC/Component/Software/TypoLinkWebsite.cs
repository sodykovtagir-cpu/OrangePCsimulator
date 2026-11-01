using PC.Component.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace PC.Component.Software
{
    // Optional binder for player-authored Website prefabs. Drag UI inputs to
    // inspector fields; wire Button.OnClick to these no-argument methods.
    // Temporary code-built pages work even if no such prefab exists.
    public sealed class TypoLinkWebsite : Website
    {
        [SerializeField] private InputField ssidInput;
        [SerializeField] private InputField passwordInput;
        [SerializeField] private InputField routerIdInput;
        [SerializeField] private Text statusText;

        private InternetService Service => InternetService.Instance;
        private PC.Component.Motherboard Board => os != null ? os.Board : null;
        private RouterDevice LocalRouter
        {
            get
            {
                if (Service != null && Service.CanOpenLocalPanel(Board, out var router)) return router;
                return null;
            }
        }

        private void Start() { Refresh(); }
        private void Say(string message) { if (statusText != null) statusText.text = message; }
        private int TargetId()
        {
            return routerIdInput != null && int.TryParse(routerIdInput.text, out var id) ? id : 0;
        }
        public void Refresh()
        {
            var service = Service;
            var router = LocalRouter;
            Say(service == null ? "Интернет недоступен" :
                "Тариф: " + service.PlanName + " | трафик: " +
                service.TotalMegabytes.ToString("0.0") + " МБ | роутер: " +
                (router != null ? router.Ssid : "не подключен"));
        }
        public void ChooseOgryzok() { ChoosePlan(1); }
        public void ChooseBasic() { ChoosePlan(2); }
        public void ChooseLuxury() { ChoosePlan(3); }
        public void ChoosePlan(int id)
        {
            if (Service == null || LocalRouter == null || !Service.SetPlan(id))
            { Say("Сначала подключись к роутеру"); return; }
            Refresh();
        }
        public void SaveWifiName()
        {
            if (LocalRouter == null || ssidInput == null || !LocalRouter.SetSsid(ssidInput.text))
            { Say("Имя Wi-Fi должно быть длиной 1–32 символа"); return; }
            Refresh();
        }
        public void SaveWifiPassword()
        {
            if (LocalRouter == null || passwordInput == null || !LocalRouter.SetPassword(passwordInput.text))
            { Say("Пароль Wi-Fi должен быть длиной 8–63 символа"); return; }
            Refresh();
        }
        public void JoinWifi()
        {
            if (Service == null || ssidInput == null || passwordInput == null ||
                !Service.JoinWifi(Board, ssidInput.text, passwordInput.text))
            { Say("Сеть не найдена или пароль неправильный"); return; }
            Refresh();
        }
        public void JoinLan()
        {
            if (Service == null || !Service.JoinLan(Board, TargetId()))
            { Say("Роутер недоступен, расстояние слишком велико или порты заняты"); return; }
            Refresh();
        }
        public void DisconnectPc()
        {
            if (Board != null) Board.ConfigureLan(0);
            Refresh();
        }
        public void AttachRepeaterLan() { AttachRepeater(false); }
        public void AttachRepeaterWifi() { AttachRepeater(true); }
        private void AttachRepeater(bool wifi)
        {
            var parent = LocalRouter;
            var child = Service != null ? Service.Router(TargetId()) : null;
            if (parent == null || child == null || !child.ConnectUpstream(parent.Id, wifi, parent.Password))
            { Say("Повторитель: слишком далеко, нет порта или возник цикл"); return; }
            Refresh();
        }
        public void SetProviderGateway()
        {
            if (Service == null || LocalRouter == null || !Service.SetGateway(LocalRouter.Id))
            { Say("Не удалось назначить шлюз"); return; }
            Refresh();
        }
    }
}
