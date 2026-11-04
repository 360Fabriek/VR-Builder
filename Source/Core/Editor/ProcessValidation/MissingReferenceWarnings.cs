using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using VRBuilder.Core.Cloning;
using VRBuilder.Core.Behaviors;
using VRBuilder.Core.Conditions;
using VRBuilder.Core.Configuration;
using VRBuilder.Core.EntityOwners;
using VRBuilder.Core.SceneObjects;

namespace VRBuilder.Core.Editor.ProcessValidation
{
    /// <summary>
    /// Shared graph checks for editor warnings and scene-isolated build validation.
    /// </summary>
    internal static class MissingReferenceWarnings
    {
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        private static readonly ReferenceComparer Comparer = new ReferenceComparer();
        private static readonly Dictionary<Type, MemberInfo[]> Members = new Dictionary<Type, MemberInfo[]>();
        private static readonly Dictionary<object, string> Results = new Dictionary<object, string>(Comparer);
        private static IProcess cachedProcess;
        private static Inspection cachedInspection;
        private static DateTime expires;

        public static bool HasWarnings(IEntity entity, IProcess process) => string.IsNullOrEmpty(GetTooltip(entity, process)) == false;

        public static string GetTooltip(IEntity entity, IProcess process)
        {
            if (entity == null) return null;
            if (ReferenceEquals(process, cachedProcess) == false || DateTime.UtcNow >= expires)
            {
                cachedProcess = process;
                expires = DateTime.UtcNow.AddMilliseconds(500);
                Results.Clear();
                cachedInspection = new Inspection(process, guid => RuntimeConfigurator.Configuration.SceneObjectRegistry.GetObjects(guid));
            }
            if (Results.TryGetValue(entity, out string tooltip) == false)
            {
                bool isGroup = entity is IStep step && step.Data?.Behaviors?.Data?.Behaviors?.Any(
                    behavior => behavior is ExecuteChapterBehavior || behavior is ExecuteChaptersBehavior) == true;
                tooltip = entity is IStep regularStep && isGroup == false
                    ? cachedInspection.GetStepTooltip(regularStep)
                    : cachedInspection.Inspect(entity, entity.GetType(), entity.GetType().Name, new HashSet<object>(Comparer)).Any()
                        ? "1+ steps have validation issues." : null;
                Results[entity] = tooltip;
            }
            return tooltip;
        }

        /// <summary>
        /// Validates without editor caches or the global scene registry, so another open scene cannot satisfy a reference.
        /// </summary>
        internal static IReadOnlyList<string> Validate(IProcess process, Func<Guid, IEnumerable<ISceneObject>> resolveObjects)
        {
            if (process?.Data?.Chapters == null) return new[] { "Process data or chapters are missing." };
            Inspection inspection = new Inspection(process, resolveObjects);
            return inspection.StructuralIssues.Concat(inspection.Inspect(process, typeof(IProcess),
                $"Process '{process.Data.Name}'", new HashSet<object>(Comparer))).ToArray();
        }

        private sealed class Inspection
        {
            private readonly HashSet<Guid> entityIds = new HashSet<Guid>();
            private readonly HashSet<object> unreachableSteps = new HashSet<object>(Comparer);
            private readonly Func<Guid, IEnumerable<ISceneObject>> resolveObjects;
            internal readonly List<string> StructuralIssues = new List<string>();

            internal Inspection(IProcess process, Func<Guid, IEnumerable<ISceneObject>> resolveObjects)
            {
                this.resolveObjects = resolveObjects ?? throw new ArgumentNullException(nameof(resolveObjects));
                CollectIds(process, new HashSet<object>(Comparer));
            }

            private void CollectIds(IEntity entity, HashSet<object> visited)
            {
                if (entity == null || visited.Add(entity) == false) return;
                if (entity.Id == Guid.Empty || entityIds.Add(entity.Id) == false)
                    StructuralIssues.Add($"{entity.GetType().Name}: empty or duplicate entity ID '{entity.Id}'.");
                if (entity is IChapter chapter && chapter.Data?.Steps != null) CollectUnreachableSteps(chapter);
                if (entity is IDataOwner owner && owner.Data is IEntityCollectionData collection)
                {
                    IEnumerable<IEntity> children;
                    try { children = collection.GetChildren().ToArray(); }
                    catch (Exception exception)
                    {
                        StructuralIssues.Add($"{entity.GetType().Name}: invalid child collection: {exception.Message}");
                        return;
                    }
                    foreach (IEntity child in children) CollectIds(child, visited);
                }
            }

            private void CollectUnreachableSteps(IChapter chapter)
            {
                HashSet<object> steps = new HashSet<object>(chapter.Data.Steps.Cast<object>(), Comparer);
                HashSet<object> reachable = new HashSet<object>(Comparer);
                Stack<IStep> pending = new Stack<IStep>();
                if (chapter.Data.FirstStep != null) pending.Push(chapter.Data.FirstStep);
                while (pending.Count > 0)
                {
                    IStep step = pending.Pop();
                    if (steps.Contains(step) == false || reachable.Add(step) == false) continue;
                    if (step.Data?.Transitions?.Data?.Transitions == null) continue;
                    foreach (ITransition transition in step.Data.Transitions.Data.Transitions)
                    {
                        IStep target = transition?.Data?.TargetStepReference?.Entity;
                        if (target != null) pending.Push(target);
                    }
                }
                foreach (IStep step in chapter.Data.Steps)
                    if (step != null && reachable.Contains(step) == false) unreachableSteps.Add(step);
            }

            internal string GetStepTooltip(IStep step)
            {
                List<string> reasons = new List<string>();
                int behaviors = step.Data?.Behaviors?.Data?.Behaviors?.Count(behavior =>
                    Inspect(behavior, typeof(IBehavior), "Behavior", new HashSet<object>(Comparer)).Any()) ?? 0;
                if (behaviors > 0)
                    reasons.Add(behaviors == 1 ? "1 behaviour has missing references." : $"{behaviors} behaviours have missing references.");

                ITransition[] transitions = step.Data?.Transitions?.Data?.Transitions?.ToArray() ?? Array.Empty<ITransition>();
                int conditions = transitions.Where(transition => transition?.Data?.Conditions != null)
                    .SelectMany(transition => transition.Data.Conditions).Count(condition =>
                        Inspect(condition, typeof(ICondition), "Condition", new HashSet<object>(Comparer)).Any());
                if (conditions > 0)
                    reasons.Add(conditions == 1 ? "1 condition has missing references." : $"{conditions} conditions have missing references.");

                int targets = transitions.Count(transition => transition?.Data?.TargetStepReference is EntityReference target
                    && target.ReferencedId != Guid.Empty && entityIds.Contains(target.ReferencedId) == false);
                if (targets > 0)
                    reasons.Add(targets == 1 ? "1 transition has a missing target reference." : $"{targets} transitions have missing target references.");
                if (unreachableSteps.Contains(step)) reasons.Add("Step is unreachable.");
                return reasons.Count > 0 ? string.Join("\n", reasons)
                    : Inspect(step, typeof(IStep), "Step", new HashSet<object>(Comparer)).FirstOrDefault();
            }

            internal IEnumerable<string> Inspect(object value, Type declaredType, string path, HashSet<object> visited)
            {
                if (value == null)
                {
                    if (typeof(ProcessSceneReferenceBase).IsAssignableFrom(declaredType)
                        || typeof(EntityReference).IsAssignableFrom(declaredType) || typeof(IEntity).IsAssignableFrom(declaredType))
                        yield return $"{path}: reference is not set.";
                    yield break;
                }
                if (value is ProcessSceneReferenceBase sceneReference)
                {
                    if (sceneReference.IsEmpty()) yield return $"{path}: scene reference is not set.";
                    else
                    {
                        Type requiredType = sceneReference.GetReferenceType();
                        foreach (Guid guid in sceneReference.Guids)
                        {
                            ISceneObject[] objects = resolveObjects(guid).ToArray();
                            if (objects.Length == 0) yield return $"{path}: scene reference '{guid}' cannot be found in this scene.";
                            foreach (ISceneObject sceneObject in objects)
                            {
                                if (sceneObject == null || sceneObject.GameObject == null)
                                    yield return $"{path}: scene reference '{guid}' resolves to a missing object.";
                                else if (requiredType.IsInstanceOfType(sceneObject) == false && sceneObject.GameObject.GetComponent(requiredType) == null)
                                    yield return $"{path}: '{sceneObject.GameObject.name}' is missing required component '{requiredType.Name}'.";
                            }
                        }
                    }
                    yield break;
                }
                if (value is EntityReference entityReference)
                {
                    if (entityReference.ReferencedId == Guid.Empty || entityIds.Contains(entityReference.ReferencedId) == false)
                        yield return $"{path}: entity reference '{entityReference.ReferencedId}' is unset or missing from the process.";
                    yield break;
                }
                if (value is string || value.GetType().IsValueType || visited.Add(value) == false) yield break;
                if (value is IProcess process)
                {
                    if (process.Data?.Chapters == null) yield return $"{path}: process data or chapters are missing.";
                    else
                    {
                        if (process.Data.Chapters.Count == 0) yield return $"{path}: process has no chapters.";
                        foreach (IChapter chapter in process.Data.Chapters)
                            foreach (string issue in Inspect(chapter, typeof(IChapter), $"{path}/Chapter '{chapter?.Data?.Name}'", visited)) yield return issue;
                    }
                    yield break;
                }
                if (value is IChapter chapterValue)
                {
                    if (chapterValue.Data?.Steps == null) yield return $"{path}: chapter data or steps are missing.";
                    else
                    {
                        if (chapterValue.Data.Steps.Count > 0 && (chapterValue.Data.FirstStep == null || chapterValue.Data.Steps.Contains(chapterValue.Data.FirstStep) == false))
                            yield return $"{path}: chapter entry does not point to a step in this chapter.";
                        foreach (IStep step in chapterValue.Data.Steps)
                            foreach (string issue in Inspect(step, typeof(IStep), $"{path}/Step '{step?.Data?.Name}'", visited)) yield return issue;
                    }
                    yield break;
                }
                if (value is IStep stepValue)
                {
                    if (unreachableSteps.Contains(stepValue)) yield return $"{path}: step is unreachable from its chapter entry.";
                    if (stepValue.Data == null) yield return $"{path}: step data is missing.";
                    else
                    {
                        if (stepValue.Data.Behaviors != null)
                            foreach (string issue in Inspect(stepValue.Data.Behaviors, typeof(IBehaviorCollection), path, visited)) yield return issue;
                        if (stepValue.Data.Transitions != null)
                            foreach (string issue in Inspect(stepValue.Data.Transitions, typeof(ITransitionCollection), path, visited)) yield return issue;
                    }
                    yield break;
                }
                if (value is IDataOwner owner)
                {
                    if (owner.Data == null) yield return $"{path}: entity data is missing.";
                    else foreach (string issue in Inspect(owner.Data, typeof(IData), path, visited)) yield return issue;
                    yield break;
                }
                if (value is IEnumerable items)
                {
                    int index = 0;
                    foreach (object item in items)
                    {
                        if (item == null) yield return $"{path}[{index}]: item is missing.";
                        else foreach (string issue in Inspect(item, item.GetType(), $"{path}[{index}]", visited)) yield return issue;
                        index++;
                    }
                    yield break;
                }
                foreach (MemberInfo member in GetMembers(value.GetType()))
                {
                    object child = member is PropertyInfo property ? property.GetValue(value) : ((FieldInfo)member).GetValue(value);
                    Type childType = member is PropertyInfo propertyInfo ? propertyInfo.PropertyType : ((FieldInfo)member).FieldType;
                    // An empty transition target intentionally ends the chapter; a dangling ID does not.
                    if (value is ITransitionData && member.Name == nameof(ITransitionData.TargetStepReference)
                        && (child == null || child is EntityReference target && target.ReferencedId == Guid.Empty)) continue;
                    foreach (string issue in Inspect(child, childType, $"{path}/{member.Name}", visited)) yield return issue;
                }
            }
        }

        private static MemberInfo[] GetMembers(Type type)
        {
            if (Members.TryGetValue(type, out MemberInfo[] members)) return members;
            List<MemberInfo> serialized = new List<MemberInfo>();
            for (Type current = type; current != null; current = current.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                serialized.AddRange(current.GetProperties(flags).Where(property => property.CanRead && property.GetIndexParameters().Length == 0
                    && property.IsDefined(typeof(DataMemberAttribute), true) && property.IsDefined(typeof(ObsoleteAttribute), true) == false));
                serialized.AddRange(current.GetFields(flags).Where(field => field.IsDefined(typeof(DataMemberAttribute), true)
                    && field.IsDefined(typeof(ObsoleteAttribute), true) == false));
            }
            members = serialized.ToArray();
            Members[type] = members;
            return members;
        }
    }
}
