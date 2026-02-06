using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemyStateMachine : MonoBehaviour
{
    [System.Serializable]
    public class StateTransition
    {
        public EnemyState fromState;
        public EnemyState toState;
        public float minDuration = 0f;
        public float maxDuration = 0f;
        public string triggerCondition;
    }

    [Header("状态机设置")]
    public EnemyState initialState = EnemyState.Idle;
    public EnemyState currentState { get; private set; }
    public List<StateTransition> stateTransitions = new List<StateTransition>();

    [Header("状态持续时间")]
    public float stateStartTime;
    public float stateDuration;

    [Header("事件")]
    public System.Action<EnemyState, EnemyState> OnStateChanged;

    private BaseEnemy enemyController;
    private Dictionary<EnemyState, System.Action> stateUpdateMethods;
    private Dictionary<EnemyState, System.Action> stateEnterMethods;
    private Dictionary<EnemyState, System.Action> stateExitMethods;

    void Awake()
    {
        enemyController = GetComponent<BaseEnemy>();
        InitializeStateMachine();
    }

    void InitializeStateMachine()
    {
        stateUpdateMethods = new Dictionary<EnemyState, System.Action>
        {
            { EnemyState.Idle, UpdateIdle },
            { EnemyState.Patrol, UpdatePatrol },
            { EnemyState.Chase, UpdateChase },
            { EnemyState.Attack, UpdateAttack },
            { EnemyState.Cooldown, UpdateCooldown },
            { EnemyState.Stunned, UpdateStunned },
            { EnemyState.Dead, UpdateDead }
        };

        stateEnterMethods = new Dictionary<EnemyState, System.Action>();
        stateExitMethods = new Dictionary<EnemyState, System.Action>();

        // 设置初始状态
        ChangeState(initialState);
    }

    void Update()
    {
        if (stateUpdateMethods.ContainsKey(currentState))
        {
            stateUpdateMethods[currentState]?.Invoke();
        }

        CheckStateTransitions();
    }

    public void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        // 调用退出方法
        if (stateExitMethods.ContainsKey(currentState))
        {
            stateExitMethods[currentState]?.Invoke();
        }

        EnemyState previousState = currentState;
        currentState = newState;

        // 重置状态计时
        stateStartTime = Time.time;
        stateDuration = GetRandomStateDuration(newState);

        // 调用进入方法
        if (stateEnterMethods.ContainsKey(currentState))
        {
            stateEnterMethods[currentState]?.Invoke();
        }

        // 触发事件
        OnStateChanged?.Invoke(previousState, newState);

        Debug.Log($"{gameObject.name} 状态改变: {previousState} -> {newState}");
    }

    public void RegisterStateCallback(EnemyState state, System.Action enterMethod = null,
                                     System.Action updateMethod = null, System.Action exitMethod = null)
    {
        if (enterMethod != null)
        {
            if (!stateEnterMethods.ContainsKey(state))
                stateEnterMethods[state] = enterMethod;
            else
                stateEnterMethods[state] += enterMethod;
        }

        if (updateMethod != null)
        {
            if (!stateUpdateMethods.ContainsKey(state))
                stateUpdateMethods[state] = updateMethod;
            else
                stateUpdateMethods[state] += updateMethod;
        }

        if (exitMethod != null)
        {
            if (!stateExitMethods.ContainsKey(state))
                stateExitMethods[state] = exitMethod;
            else
                stateExitMethods[state] += exitMethod;
        }
    }

    private void CheckStateTransitions()
    {
        foreach (var transition in stateTransitions)
        {
            if (transition.fromState == currentState)
            {
                // 检查持续时间
                float timeInState = Time.time - stateStartTime;
                if (timeInState < transition.minDuration) continue;

                // 检查最大持续时间
                if (transition.maxDuration > 0 && timeInState > transition.maxDuration)
                {
                    ChangeState(transition.toState);
                    return;
                }

                // 检查触发条件
                if (CheckTransitionCondition(transition.triggerCondition))
                {
                    ChangeState(transition.toState);
                    return;
                }
            }
        }
    }

    private bool CheckTransitionCondition(string condition)
    {
        // 这里可以实现各种条件检查
        switch (condition)
        {
            case "PlayerInRange":
                return CheckPlayerInRange(5f);
            case "HealthLow":
                return enemyController.GetHealthPercentage() < 0.3f;
            case "AttackReady":
                return Time.time - stateStartTime > 1f;
            case "":
                return true; // 无条件转换
            default:
                return false;
        }
    }

    private bool CheckPlayerInRange(float range)
    {
        // 使用公开属性而不是protected字段
        if (enemyController.PlayerTransform == null) return false;
        float distance = Vector2.Distance(transform.position, enemyController.PlayerTransform.position);
        return distance <= range;
    }

    private float GetRandomStateDuration(EnemyState state)
    {
        foreach (var transition in stateTransitions)
        {
            if (transition.fromState == state && transition.maxDuration > 0)
            {
                return Random.Range(transition.minDuration, transition.maxDuration);
            }
        }
        return 0f;
    }

    #region 基础状态更新方法

    private void UpdateIdle()
    {
        // 空闲状态基础逻辑
    }

    private void UpdatePatrol()
    {
        // 巡逻状态基础逻辑
    }

    private void UpdateChase()
    {
        // 追逐状态基础逻辑
    }

    private void UpdateAttack()
    {
        // 攻击状态基础逻辑
    }

    private void UpdateCooldown()
    {
        // 冷却状态基础逻辑
    }

    private void UpdateStunned()
    {
        // 眩晕状态基础逻辑
    }

    private void UpdateDead()
    {
        // 死亡状态基础逻辑
    }

    #endregion
}