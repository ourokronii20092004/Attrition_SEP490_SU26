
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class PlayerAnimatorBuilder
{
    [MenuItem("Tools/Build Player Animator")]
    public static void Build()
    {
        string path = "Assets/Animations/MainCharacter/Player.controller";
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);

        // === PARAMETERS ===
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("yVelocity", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("IsDead", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsAttacking", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsDashing", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("IsChargingAttack", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("ChargeAttack", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("CrouchAttack", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        // === LOAD ANIMATION CLIPS ===
        var clipIdle    = Load("Assets/Animations/MainCharacter/Player_Idle.anim");
        var clipRun     = Load("Assets/Animations/MainCharacter/Player_Run.anim");
        var clipAtk1    = Load("Assets/Animations/MainCharacter/Player_Attack1.anim");
        var clipAtk2    = Load("Assets/Animations/MainCharacter/Player_Attack2.anim");
        var clipJump    = Load("Assets/Animations/MainCharacter/Player_Jump.anim");
        var clipFall    = Load("Assets/Animations/MainCharacter/Player_JumpFall.anim");
        var clipHit     = Load("Assets/Animations/MainCharacter/Player_Hit.anim");
        var clipDeath   = Load("Assets/Animations/MainCharacter/Player_Death.anim");
        var clipCrouch  = Load("Assets/Animations/MainCharacter/Player_Crounch.anim");
        var clipCrouchW = Load("Assets/Animations/MainCharacter/Player_Crounch_Walk.anim");
        var clipCrouchA = Load("Assets/Animations/MainCharacter/Player_Crounch_Attack.anim");
        var clipDash    = Load("Assets/Animations/MainCharacter/Player_Dash.anim");

        // === STATES ===
        var sIdle    = sm.AddState("Player_Idle",        new Vector3(250, 0, 0));
        var sRun     = sm.AddState("Player_Run",         new Vector3(500, 0, 0));
        var sAtk1    = sm.AddState("Player_Attack1",     new Vector3(250, 150, 0));
        var sAtk2    = sm.AddState("Player_Attack2",     new Vector3(500, 150, 0));
        var sJump    = sm.AddState("Player_Jump",        new Vector3(0, 300, 0));
        var sFall    = sm.AddState("Player_JumpFall",    new Vector3(250, 300, 0));
        var sHit     = sm.AddState("Player_Hit",         new Vector3(500, 300, 0));
        var sDeath   = sm.AddState("Player_Death",       new Vector3(750, 300, 0));
        var sCrouch  = sm.AddState("Player_Crounch",     new Vector3(0, -150, 0));
        var sCrouchW = sm.AddState("Player_Crounch_Walk",new Vector3(250, -150, 0));
        var sCrouchA = sm.AddState("Player_Crounch_Atk", new Vector3(500, -150, 0));
        var sDash    = sm.AddState("Player_Dash",        new Vector3(750, 150, 0));

        sIdle.motion = clipIdle;   sRun.motion = clipRun;
        sAtk1.motion = clipAtk1;   sAtk2.motion = clipAtk2;
        sJump.motion = clipJump;   sFall.motion = clipFall;
        sHit.motion = clipHit;     sDeath.motion = clipDeath;
        sCrouch.motion = clipCrouch; sCrouchW.motion = clipCrouchW;
        sCrouchA.motion = clipCrouchA;
        sDash.motion = clipDash;

        sm.defaultState = sIdle;

        // === STATE TRANSITIONS ===

        // Idle → Run (Speed > 0.1)
        var t = sIdle.AddTransition(sRun);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Idle → Crouch (IsCrouching)
        t = sIdle.AddTransition(sCrouch);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

        // Run → Idle (Speed < 0.1)
        t = sRun.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Run → Crouch (IsCrouching)
        t = sRun.AddTransition(sCrouch);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

        // Jump → JumpFall (yVelocity < 0)
        t = sJump.AddTransition(sFall);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.Less, 0, "yVelocity");

        // JumpFall → Idle (IsGrounded)
        t = sFall.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");

        // Attack1 → Idle (ExitTime)
        t = sAtk1.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0;

        // Attack2 → Idle (ExitTime)
        t = sAtk2.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 0.95f; t.duration = 0;

        // Crouch → Idle (!IsCrouching)
        t = sCrouch.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Crouch → Run (!IsCrouching AND Speed > 0.1)
        t = sCrouch.AddTransition(sRun);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // Crouch → CrouchWalk (Speed > 0.1 AND IsCrouching)
        t = sCrouch.AddTransition(sCrouchW);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");

        // CrouchWalk → Crouch (Speed < 0.1)
        t = sCrouchW.AddTransition(sCrouch);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // CrouchWalk → Idle (!IsCrouching AND Speed < 0.1)
        t = sCrouchW.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // CrouchWalk → Run (!IsCrouching AND Speed > 0.1)
        t = sCrouchW.AddTransition(sRun);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");
        t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        // CrouchAttack → Crouch (ExitTime)
        t = sCrouchA.AddTransition(sCrouch);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0;

        // Dash → Idle (!IsDashing)
        t = sDash.AddTransition(sIdle);
        t.hasExitTime = false; t.duration = 0; t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");


        // Hit → Idle (ExitTime)
        t = sHit.AddTransition(sIdle);
        t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0;

        // === ANY STATE TRANSITIONS ===

        // AnyState → Jump (!IsGrounded AND yVelocity > 0 AND !IsAttacking AND !IsDashing)
        t = sm.AddAnyStateTransition(sJump);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Greater, 0, "yVelocity");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");

        // AnyState → JumpFall (!IsGrounded AND yVelocity < 0 AND !IsAttacking AND !IsDashing)
        t = sm.AddAnyStateTransition(sFall);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
        t.AddCondition(AnimatorConditionMode.Less, 0, "yVelocity");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsAttacking");
        t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDashing");

        // AnyState → Attack1 (Attack trigger)
        t = sm.AddAnyStateTransition(sAtk1);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        // AnyState → ChargeAttack (ChargeAttack trigger)
        t = sm.AddAnyStateTransition(sAtk2);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "ChargeAttack");

        // AnyState → CrouchAttack (CrouchAttack trigger)
        t = sm.AddAnyStateTransition(sCrouchA);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "CrouchAttack");

        // AnyState → Hit (Hit trigger)
        t = sm.AddAnyStateTransition(sHit);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "Hit");

        // AnyState → Death (IsDead)
        t = sm.AddAnyStateTransition(sDeath);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsDead");

        // AnyState → Dash (IsDashing)
        t = sm.AddAnyStateTransition(sDash);
        t.hasExitTime = false; t.duration = 0; t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, "IsDashing");

        // Death state: no transitions out (dead-end)

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("✅ Player Animator Controller đã được tạo thành công!");
    }

    static AnimationClip Load(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) Debug.LogWarning($"Không tìm thấy animation: {path}");
        return clip;
    }
}
#endif
