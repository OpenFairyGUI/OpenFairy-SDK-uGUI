namespace OpenFairy.UGUI.Tests
{
    public sealed class ButtonAliasTestPanel : NamedStateButton<ButtonAliasTestPanel.State>
    {
        public enum State { up, down, over }
        public ref Controller<State> state => ref controller;
    }
}
