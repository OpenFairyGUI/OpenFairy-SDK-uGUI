using System;

namespace OpenFairy.UGUI.Tests
{
    // Hand-written test buttons map states by enum member name; the importer emits the same map statically.
    public abstract class NamedStateButton<T> : Button<T> where T : struct, Enum
    {
        protected override T? StatePage(VisualState state)
        {
            var name = state.ToString();
            return Enum.TryParse(char.ToLowerInvariant(name[0]) + name[1..], out T page) ? page : null;
        }
    }

    public abstract class NamedStateCombo<T> : ComboBox<T> where T : struct, Enum
    {
        protected override T? StatePage(VisualState state)
        {
            var name = state.ToString();
            return Enum.TryParse(char.ToLowerInvariant(name[0]) + name[1..], out T page) ? page : null;
        }
    }
}
