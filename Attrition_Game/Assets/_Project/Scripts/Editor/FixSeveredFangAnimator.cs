using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class FixSeveredFangAnimator
{
    [MenuItem("Tools/Fix SeveredFang Animator")]
    public static void FixAnimator()
    {
        string path = "Assets/_Project/Animations/SeveredFang/SeveredFangIdle001-Sheet_0 1.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        
        if (controller == null)
        {
            Debug.LogError("Could not find AnimatorController at " + path);
            return;
        }

        // 1. Add Parameters
        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Sheathe", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Hurt", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "DieTrigger", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);

        // 2. Fix States and Transitions
        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Clear existing entry and any state transitions
        rootStateMachine.entryTransitions = new AnimatorTransition[0];
        rootStateMachine.anyStateTransitions = new AnimatorStateTransition[0];

        AnimatorState idleState = null;
        AnimatorState walkState = null;
        AnimatorState attackState = null;
        AnimatorState sheatheState = null;
        AnimatorState hurtState = null;
        AnimatorState deathState = null;

        // Find states and remove their exit transitions temporarily to rebuild them
        foreach (var stateNode in rootStateMachine.states)
        {
            var state = stateNode.state;
            state.transitions = new AnimatorStateTransition[0]; // Clear existing transitions

            if (state.name.Contains("Idle")) idleState = state;
            if (state.name.Contains("Walk")) walkState = state;
            if (state.name.Contains("Attack")) attackState = state;
            if (state.name.Contains("Sheathe")) sheatheState = state;
            if (state.name.Contains("Hurt")) hurtState = state;
            if (state.name.Contains("Death")) deathState = state;
        }

        if (idleState != null) rootStateMachine.defaultState = idleState;

        // 3. Create Idle <-> Walk transitions
        if (idleState != null && walkState != null)
        {
            var idleToWalk = idleState.AddTransition(walkState);
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            idleToWalk.hasExitTime = false;
            idleToWalk.duration = 0f;

            var walkToIdle = walkState.AddTransition(idleState);
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            walkToIdle.hasExitTime = false;
            walkToIdle.duration = 0f;
        }

        // 4. Create AnyState -> Action transitions
        if (attackState != null)
        {
            var anyToAttack = rootStateMachine.AddAnyStateTransition(attackState);
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0f;

            var attackExit = attackState.AddExitTransition();
            attackExit.hasExitTime = true;
            attackExit.exitTime = 0.9f;
            attackExit.duration = 0f;
        }

        if (sheatheState != null)
        {
            var anyToSheathe = rootStateMachine.AddAnyStateTransition(sheatheState);
            anyToSheathe.AddCondition(AnimatorConditionMode.If, 0, "Sheathe");
            anyToSheathe.hasExitTime = false;
            anyToSheathe.duration = 0f;

            var sheatheExit = sheatheState.AddExitTransition();
            sheatheExit.hasExitTime = true;
            sheatheExit.exitTime = 0.9f;
            sheatheExit.duration = 0f;
        }

        if (hurtState != null)
        {
            var anyToHurt1 = rootStateMachine.AddAnyStateTransition(hurtState);
            anyToHurt1.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            anyToHurt1.hasExitTime = false;
            anyToHurt1.duration = 0f;
            
            var anyToHurt2 = rootStateMachine.AddAnyStateTransition(hurtState);
            anyToHurt2.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            anyToHurt2.hasExitTime = false;
            anyToHurt2.duration = 0f;

            var hurtExit = hurtState.AddExitTransition();
            hurtExit.hasExitTime = true;
            hurtExit.exitTime = 0.9f;
            hurtExit.duration = 0f;
        }

        if (deathState != null)
        {
            var anyToDeath1 = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath1.AddCondition(AnimatorConditionMode.If, 0, "Death");
            anyToDeath1.hasExitTime = false;
            anyToDeath1.duration = 0f;
            
            var anyToDeath2 = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath2.AddCondition(AnimatorConditionMode.If, 0, "DieTrigger");
            anyToDeath2.hasExitTime = false;
            anyToDeath2.duration = 0f;
            
            var anyToDeath3 = rootStateMachine.AddAnyStateTransition(deathState);
            anyToDeath3.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
            anyToDeath3.hasExitTime = false;
            anyToDeath3.duration = 0f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        // Dọn transition mồ côi do việc gán lại mảng transitions ở trên để lại (sub-asset không bị
        // destroy). Bỏ bước này → file tích rác → cửa sổ Animator ném Edge.WakeUp NullReference.
        CleanOrphanTransitions(controller, path);

        Debug.Log("SeveredFang Animator Controller fixed successfully!");
    }

    /// <summary>Destroy mọi AnimatorStateTransition/AnimatorTransition không còn được tham chiếu trong controller.</summary>
    private static void CleanOrphanTransitions(AnimatorController controller, string path)
    {
        var referenced = new System.Collections.Generic.HashSet<Object>();
        foreach (var layer in controller.layers)
            CollectTransitions(layer.stateMachine, referenced);

        int removed = 0;
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj == null) continue;
            if (!(obj is AnimatorStateTransition || obj is AnimatorTransition)) continue;
            if (referenced.Contains(obj)) continue;
            Object.DestroyImmediate(obj, true);
            removed++;
        }

        if (removed > 0)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[FixSeveredFangAnimator] Dọn {removed} transition mồ côi.");
        }
    }

    private static void CollectTransitions(AnimatorStateMachine sm, System.Collections.Generic.HashSet<Object> referenced)
    {
        if (sm == null) return;
        foreach (var t in sm.anyStateTransitions) if (t != null) referenced.Add(t);
        foreach (var t in sm.entryTransitions) if (t != null) referenced.Add(t);
        foreach (var cs in sm.states)
            if (cs.state != null)
                foreach (var t in cs.state.transitions) if (t != null) referenced.Add(t);
        foreach (var csm in sm.stateMachines)
            if (csm.stateMachine != null)
            {
                foreach (var t in sm.GetStateMachineTransitions(csm.stateMachine)) if (t != null) referenced.Add(t);
                CollectTransitions(csm.stateMachine, referenced);
            }
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (var param in controller.parameters)
        {
            if (param.name == name) return; // Already exists
        }
        controller.AddParameter(name, type);
    }
}
