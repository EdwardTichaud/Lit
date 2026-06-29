using UnityEngine;

public enum BattleInputType
{
    None = 0,
    Back = 1,
    Select1 = 2,
    Select2 = 3,
    Select3 = 4,
    Confirm = 5,
    Action = 6,
    EnemiesGroupSelection = 7,
    SquadGroupSelection = 8,
    Awake = 9,
    LeftShoulder = 10,
    RightShoulder = 11,
    BaseAttack = 12,
    Menu = 13
}

[CreateAssetMenu(menuName = "Symphonie/QTE Input", fileName = "QTEInput")]
public class QTEInputSO : ScriptableObject
{
    [SerializeField] private Sprite inputSprite;
    [SerializeField] private BattleInputType battleInput = BattleInputType.Confirm;

    public Sprite InputSprite => inputSprite;
    public BattleInputType BattleInput => battleInput;
}
