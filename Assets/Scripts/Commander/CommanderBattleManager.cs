using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;
using Starfall.Audio;
using Starfall.VFX;

namespace Starfall.Commander
{
    public enum BattleState
    {
        PreMatch,
        InProgress,
        Overtime,
        MatchOver
    }

    /// <summary>
    /// Master coordinator for the Fleet Commander Tug-of-War PvP/PvE battle mode.
    /// Manages match timing, double-energy Overtime, base Core hitpoints,
    /// dynamic energy regeneration, and squadron unit warp deployment.
    /// </summary>
    public class CommanderBattleManager : MonoBehaviour
    {
        public static CommanderBattleManager Instance { get; private set; }

        [Header("Match Settings")]
        [SerializeField] private float matchDuration = 180f; // 3 minute match
        [SerializeField] private float overtimeThreshold = 60f; // Last 60s is Overtime
        [SerializeField] private int maxCoreHealth = 100;
        [SerializeField] private float maxEnergy = 10f;
        [SerializeField] private float normalEnergyRate = 0.95f; // +1 energy per ~1.05s
        [SerializeField] private float overtimeEnergyRate = 1.9f; // Double energy in Overtime

        // Match State
        private BattleState state = BattleState.PreMatch;
        private float timeRemaining;
        private int playerCoreHP;
        private int opponentCoreHP;
        private float playerEnergy = 4.0f; // Start with 4 opening energy
        private float opponentEnergy = 4.0f;

        // Decks & References
        private List<SquadronCardDef> playerDeck;
        private List<SquadronCardDef> opponentDeck;
        private Transform playerFlagship;
        private OpponentCommanderAI opponentCommander;

        // Public Properties
        public BattleState State => state;
        public float TimeRemaining => timeRemaining;
        public int PlayerCoreHP => playerCoreHP;
        public int OpponentCoreHP => opponentCoreHP;
        public int MaxCoreHealth => maxCoreHealth;
        public float PlayerEnergy => playerEnergy;
        public float OpponentEnergy => opponentEnergy;
        public float MaxEnergy => maxEnergy;
        public List<SquadronCardDef> PlayerDeck => playerDeck;
        public List<SquadronCardDef> OpponentDeck => opponentDeck;
        public Transform PlayerFlagshipTransform => playerFlagship;
        public Transform OpponentFlagshipTransform => opponentCommander != null ? opponentCommander.transform : null;

        // Events
        public event Action<float, float> OnPlayerEnergyChanged;
        public event Action<int, int> OnPlayerCoreHPChanged;
        public event Action<int, int> OnOpponentCoreHPChanged;
        public event Action<float> OnTimerTick;
        public event Action OnOvertimeStarted;
        public event Action<bool> OnMatchEnded; // (isPlayerWinner)
        public event Action<int, SquadronCardDef> OnCardDeployed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            playerCoreHP = maxCoreHealth;
            opponentCoreHP = maxCoreHealth;
            timeRemaining = matchDuration;

            playerDeck = CommanderCatalog.GetDefaultPlayerDeck();
            opponentDeck = CommanderCatalog.GetDefaultOpponentDeck();
        }

        private void Start()
        {
            LocateCombatants();
            StartCoroutine(StartMatchSequence());
        }

        private void LocateCombatants()
        {
            // Find player flagship
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerFlagship = playerObj.transform;
            }

            // Find or spawn opponent commander flagship
            opponentCommander = FindAnyObjectByType<OpponentCommanderAI>();
            if (opponentCommander == null)
            {
                var oppGo = new GameObject("OpponentCommanderFlagship");
                oppGo.transform.position = new Vector3(0f, PlayfieldBounds.Top - 1.25f, 0f);
                opponentCommander = oppGo.AddComponent<OpponentCommanderAI>();
                opponentCommander.Initialize(opponentDeck);
            }
        }

        private IEnumerator StartMatchSequence()
        {
            state = BattleState.PreMatch;
            yield return new WaitForSeconds(1.0f);
            state = BattleState.InProgress;
        }

        private void Update()
        {
            if (state == BattleState.PreMatch || state == BattleState.MatchOver) return;

            float dt = Time.deltaTime;

            // 1. Match Timer Countdown
            timeRemaining -= dt;
            OnTimerTick?.Invoke(Mathf.Max(0f, timeRemaining));

            if (state == BattleState.InProgress && timeRemaining <= overtimeThreshold)
            {
                TriggerOvertime();
            }

            if (timeRemaining <= 0f)
            {
                EvaluateTimeoutWinner();
                return;
            }

            // 2. Command Energy Accumulation
            float currentRate = (state == BattleState.Overtime) ? overtimeEnergyRate : normalEnergyRate;

            if (playerEnergy < maxEnergy)
            {
                playerEnergy = Mathf.Min(maxEnergy, playerEnergy + currentRate * dt);
                OnPlayerEnergyChanged?.Invoke(playerEnergy, maxEnergy);
            }

            if (opponentEnergy < maxEnergy)
            {
                opponentEnergy = Mathf.Min(maxEnergy, opponentEnergy + currentRate * dt);
            }

            // 3. Keyboard Shortcut Deployment: [1], [2], [3], [4]
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;

            if (kb.digit1Key.wasPressedThisFrame) TryDeployPlayerCard(0);
            else if (kb.digit2Key.wasPressedThisFrame) TryDeployPlayerCard(1);
            else if (kb.digit3Key.wasPressedThisFrame) TryDeployPlayerCard(2);
            else if (kb.digit4Key.wasPressedThisFrame) TryDeployPlayerCard(3);
        }

        public bool TryDeployPlayerCard(int cardIndex)
        {
            if (state == BattleState.MatchOver || playerDeck == null || cardIndex < 0 || cardIndex >= playerDeck.Count)
                return false;

            var card = playerDeck[cardIndex];
            if (playerEnergy < card.energyCost)
            {
                // Not enough energy!
                return false;
            }

            playerEnergy -= card.energyCost;
            OnPlayerEnergyChanged?.Invoke(playerEnergy, maxEnergy);
            OnCardDeployed?.Invoke(cardIndex, card);

            // Spawn friendly squadron
            Vector3 origin = playerFlagship != null ? playerFlagship.position : new Vector3(0f, -3.5f, 0f);
            SpawnSquadron(card, true, origin);

            return true;
        }

        public void DeployOpponentSquad(SquadronCardDef card)
        {
            if (state == BattleState.MatchOver || card == null) return;

            opponentEnergy = Mathf.Max(0f, opponentEnergy - card.energyCost);

            Vector3 origin = opponentCommander != null ? opponentCommander.transform.position : new Vector3(0f, 3.5f, 0f);
            SpawnSquadron(card, false, origin);
        }

        private void SpawnSquadron(SquadronCardDef card, bool isPlayerTeam, Vector3 anchorPos)
        {
            int count = Mathf.Max(1, card.spawnCount);
            float forwardOffset = isPlayerTeam ? 0.45f : -0.45f;

            // Warp flash visual
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("ShockwaveVFX", anchorPos, Quaternion.identity);
            }
            AudioManager.Instance?.PlayPlayerLaser();

            for (int i = 0; i < count; i++)
            {
                // Staggered formation offsets
                float xOffset = 0f;
                float yOffset = 0f;

                if (count == 3)
                {
                    // Delta wing trio
                    if (i == 0) { xOffset = 0f; yOffset = forwardOffset; }
                    else if (i == 1) { xOffset = -0.65f; yOffset = 0f; }
                    else if (i == 2) { xOffset = 0.65f; yOffset = 0f; }
                }
                else if (count == 2)
                {
                    xOffset = (i == 0) ? -0.45f : 0.45f;
                }

                Vector3 spawnPos = anchorPos + new Vector3(xOffset, yOffset, 0f);
                spawnPos.x = Mathf.Clamp(spawnPos.x, PlayfieldBounds.PlayLeft + 0.4f, PlayfieldBounds.PlayRight - 0.4f);

                var unitGo = new GameObject($"Squadron_{card.displayName}_{(isPlayerTeam ? "P" : "E")}_{i}");
                var unit = unitGo.AddComponent<SquadronUnit>();
                unit.Initialize(card, isPlayerTeam, spawnPos);
            }
        }

        public void DamagePlayerCore(int damage)
        {
            if (state == BattleState.MatchOver) return;

            playerCoreHP = Mathf.Max(0, playerCoreHP - damage);
            OnPlayerCoreHPChanged?.Invoke(playerCoreHP, maxCoreHealth);

            CameraShake.Instance?.Shake(0.15f, 0.25f);
            AudioManager.Instance?.PlayHit(true);

            if (playerCoreHP <= 0)
            {
                EndMatch(false); // Opponent wins
            }
        }

        public void DamageOpponentCore(int damage)
        {
            if (state == BattleState.MatchOver) return;

            opponentCoreHP = Mathf.Max(0, opponentCoreHP - damage);
            OnOpponentCoreHPChanged?.Invoke(opponentCoreHP, maxCoreHealth);

            CameraShake.Instance?.Shake(0.15f, 0.25f);
            AudioManager.Instance?.PlayHit(true);

            if (opponentCoreHP <= 0)
            {
                EndMatch(true); // Player wins!
            }
        }

        private void TriggerOvertime()
        {
            state = BattleState.Overtime;
            OnOvertimeStarted?.Invoke();
            CameraShake.Instance?.Shake(0.2f, 0.35f);
            Debug.Log("<color=yellow>[CommanderBattle] DOUBLE ENERGY OVERTIME ACTIVE!</color>");
        }

        private void EvaluateTimeoutWinner()
        {
            if (playerCoreHP > opponentCoreHP)
                EndMatch(true);
            else if (opponentCoreHP > playerCoreHP)
                EndMatch(false);
            else
                EndMatch(true); // Tie-breaker favors player in PvE
        }

        private void EndMatch(bool playerWon)
        {
            state = BattleState.MatchOver;
            OnMatchEnded?.Invoke(playerWon);

            if (playerWon)
            {
                // Award player Credits
                Starfall.Levels.LevelProgressManager.AddCredits(500);
            }
        }
    }
}
