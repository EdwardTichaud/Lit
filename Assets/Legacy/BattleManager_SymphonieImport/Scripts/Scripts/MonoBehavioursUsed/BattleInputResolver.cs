using UnityEngine.InputSystem;

public static class BattleInputResolver
{
    public static InputAction Resolve(PlayerInputs.BattleActions battle, BattleInputType input)
    {
        return input switch
        {
            BattleInputType.Back => battle.Back,
            BattleInputType.Select1 => battle.Select1,
            BattleInputType.Select2 => battle.Select2,
            BattleInputType.Select3 => battle.Select3,
            BattleInputType.Confirm => battle.Confirm,
            BattleInputType.Action => battle.Action,
            BattleInputType.EnemiesGroupSelection => battle.EnemiesGroupSelection,
            BattleInputType.SquadGroupSelection => battle.SquadGroupSelection,
            BattleInputType.Awake => battle.Awake,
            BattleInputType.LeftShoulder => battle.LeftShoulder,
            BattleInputType.RightShoulder => battle.RightShoulder,
            BattleInputType.BaseAttack => battle.BaseAttack,
            BattleInputType.Menu => battle.Menu,
            _ => null
        };
    }
}
