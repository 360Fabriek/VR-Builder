using UnityEngine;
using UnityEngine.UIElements;
using VRBuilder.Core.Editor.UI.StepInspectorUITK.Tabs.Items;

namespace VRBuilder.Core.Editor.UI.StepInspectorUITK.Tabs
{
    internal sealed class StepHeaderPanel : IStepInspectorPanel
    {
        public string Id => PanelIds.Header;
        public GUIContent Label { get; } = new GUIContent("Step");

        public VisualElement BuildContent(IStepData step)
        {
            ScrollView root = new ScrollView(ScrollViewMode.Vertical);
            root.AddToClassList("vrb-step-header");

            VisualElement nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            TextField nameField = BuildNameField(step);
            nameField.style.flexGrow = 1;
            nameField.style.flexShrink = 1;
            nameField.style.minWidth = 0;
            nameRow.Add(nameField);
            nameRow.Add(BuildHiddenField(step));
            root.Add(nameRow);
            root.Add(BuildDescriptionField(step));

            return root;
        }

        public void Dispose() { }

        private static TextField BuildNameField(IStepData step)
        {
            string capturedOld = step.Name ?? string.Empty;

            TextField field = new TextField("Step Name")
            {
                value = capturedOld,
                isDelayed = true,
                multiline = false
            };
            field.AddToClassList("vrb-field");
            field.AddToClassList("vrb-step-header__name");

            field.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                string newValue = evt.newValue ?? string.Empty;
                string oldValue = capturedOld;
                if (newValue == oldValue)
                {
                    return;
                }

                TabMutations.Do(
                    () => step.SetName(newValue),
                    () => step.SetName(oldValue));

                capturedOld = newValue;
            });

            return field;
        }

        private static Toggle BuildHiddenField(IStepData step)
        {
            Toggle field = new Toggle("Hidden")
            {
                name = "step-is-hidden",
                value = step.IsHidden,
                tooltip = "Mark this step hidden in process overviews. It still executes normally."
            };
            field.RegisterValueChangedCallback(evt =>
            {
                bool oldValue = step.IsHidden;
                bool newValue = evt.newValue;
                if (newValue == oldValue) return;

                TabMutations.Do(
                    () => step.IsHidden = newValue,
                    () => step.IsHidden = oldValue);
            });
            return field;
        }

        private static TextField BuildDescriptionField(IStepData step)
        {
            string capturedOld = step.Description ?? string.Empty;

            TextField field = new TextField("Description")
            {
                value = capturedOld,
                isDelayed = true,
                multiline = true
            };
            field.AddToClassList("vrb-field");
            field.AddToClassList("vrb-step-header__description");
            field.style.whiteSpace = WhiteSpace.Normal;

            field.RegisterCallback<ChangeEvent<string>>(evt =>
            {
                string newValue = evt.newValue ?? string.Empty;
                string oldValue = capturedOld;
                if (newValue == oldValue)
                {
                    return;
                }

                TabMutations.Do(
                    () => step.Description = newValue,
                    () => step.Description = oldValue);

                capturedOld = newValue;
            });

            return field;
        }
    }
}
