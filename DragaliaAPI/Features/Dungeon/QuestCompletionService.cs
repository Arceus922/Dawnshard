﻿using DragaliaAPI.Database.Repositories;
using DragaliaAPI.Features.Reward;
using DragaliaAPI.Models;
using DragaliaAPI.Models.Generated;
using DragaliaAPI.Services.Exceptions;
using DragaliaAPI.Shared.Definitions.Enums;
using DragaliaAPI.Shared.MasterAsset;
using DragaliaAPI.Shared.MasterAsset.Models.QuestRewards;
using Microsoft.EntityFrameworkCore;

namespace DragaliaAPI.Features.Dungeon;

public class QuestCompletionService(
    IRewardService rewardService,
    ILogger<QuestCompletionService> logger,
    IUnitRepository unitRepository
) : IQuestCompletionService
{
    public async Task<(
        IEnumerable<AtgenScoreMissionSuccessList> Missions,
        int Points,
        int BoostedPoints
    )> CompleteQuestScoreMissions(
        DungeonSession session,
        PlayRecord record,
        double abilityMultiplier
    )
    {
        // TODO: Add Enemy scoring
        List<AtgenScoreMissionSuccessList> missions = new();

        int questId = session.QuestData.Id;

        if (
            !MasterAsset.QuestScoreMissionRewardInfo.TryGetValue(
                questId,
                out QuestScoreMissionRewardInfo? info
            )
        )
        {
            return (missions, 0, 0);
        }

        double multiplier = 100.0f;

        foreach (QuestMissionCondition condition in info.RewardConditions)
        {
            // The type is called AtgenScoreMission*Success*List, but a mission with correction_value == 0 means it was not completed (so greyed out)
            int percentageAdded = await IsQuestMissionCompleted(
                condition.QuestCompleteType,
                condition.QuestCompleteValue,
                record,
                session
            )
                ? condition.PercentageAdded
                : 0;

            multiplier += percentageAdded;
            missions.Add(
                new AtgenScoreMissionSuccessList(
                    condition.QuestCompleteType,
                    condition.QuestCompleteValue,
                    percentageAdded
                )
            );
        }

        logger.LogDebug("Completed mission score missions {@missions}", missions);

        int questScoreMissionId = MasterAsset.QuestRewardData[questId].QuestScoreMissionId;
        int baseQuantity = MasterAsset.QuestScoreMissionData[questScoreMissionId].Scores[
            record.wave
        ];

        double obtainedAmount = baseQuantity * (multiplier / 100.0f);
        int rewardQuantity = (int)Math.Floor(obtainedAmount);

        EventKindType eventType = MasterAsset.EventData[
            MasterAsset.QuestData[questId].Gid
        ].EventKindType;

        await rewardService.GrantReward(
            new Entity(eventType.ToItemType(), (int)info.RewardEntityId, rewardQuantity)
        );

        int boostPoints = 0;

        if (abilityMultiplier != 1)
        {
            boostPoints = (int)Math.Floor(rewardQuantity * (abilityMultiplier - 1));

            logger.LogDebug(
                "Rewarding {boostPointQuantity} extra points due to abilities",
                boostPoints
            );

            await rewardService.GrantReward(
                new Entity(eventType.ToItemType(), (int)info.RewardEntityId, boostPoints)
            );
        }

        return (missions, rewardQuantity, boostPoints);
    }

    public async Task<QuestMissionStatus> CompleteQuestMissions(
        DungeonSession session,
        bool[] currentState,
        PlayRecord record
    )
    {
        List<AtgenMissionsClearSet> clearSet = new();

        bool[] newState = { currentState[0], currentState[1], currentState[2] };

        QuestRewardData rewardData = MasterAsset.QuestRewardData[session.QuestData.Id];
        for (int i = 0; i < 3; i++)
        {
            (QuestCompleteType type, int value) = rewardData.Missions[i];

            if (!currentState[i] && await IsQuestMissionCompleted(type, value, record, session))
            {
                newState[i] = true;
                (EntityTypes entity, int id, int quantity) = rewardData.Entities[i];
                await rewardService.GrantReward(new Entity(entity, id, quantity));
                logger.LogDebug("Completed quest mission {missionId}", i);
                clearSet.Add(new AtgenMissionsClearSet(id, entity, quantity, i + 1));
            }
        }

        List<AtgenFirstClearSet> completeSet = new();

        if (currentState.Any(x => !x) && newState.All(x => x))
        {
            await rewardService.GrantReward(
                new Entity(
                    rewardData.MissionCompleteEntityType,
                    rewardData.MissionCompleteEntityId,
                    rewardData.MissionCompleteEntityQuantity
                )
            );
            logger.LogDebug("Granting bonus for completing all missions");
            completeSet.Add(
                new AtgenFirstClearSet(
                    rewardData.MissionCompleteEntityId,
                    rewardData.MissionCompleteEntityType,
                    rewardData.MissionCompleteEntityQuantity
                )
            );
        }

        return new(newState, clearSet, completeSet);
    }

    public async Task<bool> IsQuestMissionCompleted(
        QuestCompleteType type,
        int completionValue,
        PlayRecord record,
        DungeonSession session
    )
    {
        IEnumerable<PartySettingList> party = session.Party;

        return type switch
        {
            QuestCompleteType.None => false,
            QuestCompleteType.LimitFall => record.down_count <= completionValue,
            QuestCompleteType.DefeatAllEnemies
                => record.treasure_record.All(
                    x =>
                        x.enemy == null
                        || !session.EnemyList[x.area_idx]
                            .Select(y => y.enemy_idx)
                            .Except(x.enemy)
                            .Any()
                ), // (Maybe)TODO
            QuestCompleteType.MaxTeamSize => party.Count() <= completionValue,
            QuestCompleteType.AdventurerElementRequired
                => party.All(
                    x =>
                        MasterAsset.CharaData[x.chara_id].ElementalType
                        == (UnitElement)completionValue
                ),
            QuestCompleteType.AdventurerElementNeeded
                => party.Any(
                    x =>
                        MasterAsset.CharaData[x.chara_id].ElementalType
                        == (UnitElement)completionValue
                ),
            QuestCompleteType.AdventurerElementLocked
                => party.All(
                    x =>
                        MasterAsset.CharaData[x.chara_id].ElementalType
                        != (UnitElement)completionValue
                ),
            QuestCompleteType.DragonElementRequired
                => await IsDragonConditionMet(type, completionValue, party),
            QuestCompleteType.DragonElementNeeded
                => await IsDragonConditionMet(type, completionValue, party),
            QuestCompleteType.DragonElementLocked
                => await IsDragonConditionMet(type, completionValue, party),
            QuestCompleteType.WeaponRequired
                => party.All(x => x.equip_weapon_body_id == (WeaponBodies)completionValue),
            QuestCompleteType.MaxTraps => record.trap_count <= completionValue,
            QuestCompleteType.MaxAfflicted => record.bad_status <= completionValue,
            QuestCompleteType.TreasureChestCount
                => record.treasure_record.Count() >= completionValue,
            QuestCompleteType.AllTreasureChestsOpened => true, // TODO
            QuestCompleteType.NoContinues => record.play_continue_count == 0,
            QuestCompleteType.AdventurerRequired
                => party.Any(x => x.chara_id == (Charas)completionValue),
            QuestCompleteType.AllEnemyParts => true, // TODO
            QuestCompleteType.MaxTime => record.time < completionValue,
            QuestCompleteType.MaxDamageTimes => record.damage_count <= completionValue,
            QuestCompleteType.MinShapeshift => record.dragon_transform_count >= completionValue,
            QuestCompleteType.DefeatImperialCommander
                => record.treasure_record.Any(
                    x =>
                        x.enemy.Any(
                            y => y == 500200001 /* Imperial Commander */
                        )
                ),
            QuestCompleteType.DefeatMinBandits
                => record.treasure_record.Sum(
                    x =>
                        x.enemy.Count(
                            y => y == 500210001 /* Bandit */
                        )
                ) >= completionValue,
            QuestCompleteType.DefeatShadowKnight
                => record.treasure_record.Any(
                    x =>
                        x.enemy.Any(
                            y => y == 500170001 /* Shadow Knight */
                        )
                ),
            QuestCompleteType.SaveMinHouses => record.visit_private_house >= completionValue,
            QuestCompleteType.MinGateHp => record.protection_damage >= completionValue,
            QuestCompleteType.MinTimeRemaining => record.remaining_time >= completionValue,
            QuestCompleteType.MinDrawbridgesLowered
                => record.lower_drawbridge_count >= completionValue,
            QuestCompleteType.FireEmblemAdventurerNeeded
                => party.Any(
                    x =>
                        x.chara_id
                            is Charas.Alfonse
                                or Charas.Veronica
                                or Charas.Fjorm
                                or Charas.Marth
                                or Charas.Sharena
                                or Charas.Peony
                                or Charas.Tiki
                                or Charas.Chrom
                ),
            QuestCompleteType.MinStarsAdventurerNeeded
                => party.Any(x => MasterAsset.CharaData[x.chara_id].Rarity == completionValue),
            QuestCompleteType.MinAdventurersWithSameWeapon
                => party
                    .ToLookup(x => MasterAsset.CharaData[x.chara_id].WeaponType)
                    .Any(x => x.Count() >= completionValue),
            QuestCompleteType.MaxShapeshift => record.dragon_transform_count <= completionValue,
            QuestCompleteType.NoRevives => record.reborn_count == 0,
            QuestCompleteType.DefeatFormaChrom
                => record.treasure_record.Any(
                    x =>
                        x.enemy.Any(
                            y => y == 601500002 /* Forma Chrom */
                        )
                ),
            _
                => throw new DragaliaException(
                    ResultCode.CommonInvalidArgument,
                    "Invalid QuestCompleteType"
                )
        };
    }

    private async Task<bool> IsDragonConditionMet(
        QuestCompleteType type,
        int value,
        IEnumerable<PartySettingList> party
    )
    {
        IEnumerable<long> dragonKeyIds = party.Select(x => (long)x.equip_dragon_key_id).ToList();

        IEnumerable<Dragons> dragons = await unitRepository.Dragons
            .Where(x => dragonKeyIds.Contains(x.DragonKeyId))
            .Select(x => x.DragonId)
            .ToListAsync();

        return type switch
        {
            QuestCompleteType.DragonElementRequired
                => dragons.Any(x => MasterAsset.DragonData[x].ElementalType == (UnitElement)value),
            QuestCompleteType.DragonElementNeeded
                => dragons.All(x => MasterAsset.DragonData[x].ElementalType == (UnitElement)value),
            QuestCompleteType.DragonElementLocked
                => dragons.All(x => MasterAsset.DragonData[x].ElementalType != (UnitElement)value),
            _ => throw new NotImplementedException(),
        };
    }

    public async Task<IEnumerable<AtgenFirstClearSet>> GrantFirstClearRewards(int questId)
    {
        List<AtgenFirstClearSet> rewards = new();

        QuestRewardData rewardData = MasterAsset.QuestRewardData[questId];
        foreach (
            Entity rewardEntity in rewardData.FirstClearEntities
                .Where(x => x.Type != EntityTypes.None)
                .Select(x => new Entity(x.Type, x.Id, x.Quantity))
        )
        {
            await rewardService.GrantReward(rewardEntity);
            rewards.Add(
                new AtgenFirstClearSet(rewardEntity.Id, rewardEntity.Type, rewardEntity.Quantity)
            );
        }

        return rewards;
    }
}
