using DragaliaAPI.Database.Entities;
using DragaliaAPI.Database.Utils;
using DragaliaAPI.Shared.Definitions.Enums;
using DragaliaAPI.Shared.MasterAsset;
using DragaliaAPI.Shared.MasterAsset.Models.Missions;
using Microsoft.EntityFrameworkCore;

namespace DragaliaAPI.Features.Missions;

public class MissionProgressionService(
    IMissionRepository missionRepository,
    ILogger<MissionProgressionService> logger
) : IMissionProgressionService
{
    private readonly Queue<MissionEvent> eventQueue = new();

    public void OnQuestCleared(
        int questId,
        int questGroupId,
        QuestPlayModeTypes playMode,
        int count,
        int total
    )
    {
        EnqueueEvent(
            MissionCompleteType.QuestCleared,
            count,
            total,
            questId,
            questGroupId,
            (int)playMode
        );
    }

    public void OnEventGroupCleared(
        int eventGroupId,
        VariationTypes type,
        QuestPlayModeTypes playMode,
        int count,
        int total
    )
    {
        EnqueueEvent(
            MissionCompleteType.EventGroupCleared,
            count,
            total,
            eventGroupId,
            (int)type,
            (int)playMode
        );
    }

    public void OnQuestStoryCleared(int id)
    {
        EnqueueEvent(MissionCompleteType.QuestStoryCleared, parameter: id);
    }

    public void OnWeaponEarned(
        WeaponBodies weapon,
        UnitElement element,
        int rarity,
        WeaponSeries series
    )
    {
        EnqueueEvent(
            MissionCompleteType.WeaponEarned,
            1,
            1,
            (int)weapon,
            (int)element,
            rarity,
            (int)series
        );
    }

    public void OnWeaponRefined(
        int count,
        int total,
        WeaponBodies weapon,
        UnitElement element,
        int rarity,
        WeaponSeries series
    )
    {
        EnqueueEvent(
            MissionCompleteType.WeaponRefined,
            count,
            total,
            (int)weapon,
            (int)element,
            rarity,
            (int)series
        );
    }

    public void OnAbilityCrestBuildupPlusCount(
        AbilityCrests crest,
        PlusCountType type,
        int count,
        int total
    )
    {
        EnqueueEvent(
            MissionCompleteType.AbilityCrestBuildupPlusCount,
            count,
            total,
            (int)crest,
            (int)type
        );
    }

    public void OnAbilityCrestTotalPlusCountUp(AbilityCrests crest, int count, int total)
    {
        EnqueueEvent(MissionCompleteType.AbilityCrestTotalPlusCountUp, count, total, (int)crest);
    }

    public void OnAbilityCrestLevelUp(AbilityCrests crest, int count, int totalLevel)
    {
        EnqueueEvent(MissionCompleteType.AbilityCrestLevelUp, count, totalLevel, (int)crest);
    }

    public void OnCharacterBuildupPlusCount(
        Charas chara,
        UnitElement element,
        PlusCountType type,
        int count,
        int total
    )
    {
        EnqueueEvent(
            MissionCompleteType.CharacterBuildupPlusCount,
            count,
            total,
            (int)chara,
            (int)element,
            (int)type
        );
    }

    public void OnCharacterLevelUp(Charas chara, UnitElement element, int count, int totalLevel)
    {
        EnqueueEvent(
            MissionCompleteType.CharacterLevelUp,
            count,
            totalLevel,
            (int)chara,
            (int)element
        );
    }

    public void OnCharacterManaNodeUnlock(Charas chara, UnitElement element, int count, int total)
    {
        EnqueueEvent(
            MissionCompleteType.CharacterManaNodeUnlock,
            count,
            total,
            (int)chara,
            (int)element
        );
    }

    public void OnDragonLevelUp(Dragons dragon, UnitElement element, int count, int total)
    {
        EnqueueEvent(MissionCompleteType.DragonLevelUp, count, total, (int)dragon, (int)element);
    }

    public void OnDragonGiftSent(
        Dragons dragon,
        DragonGifts gift,
        UnitElement element,
        int count,
        int total
    )
    {
        EnqueueEvent(
            MissionCompleteType.DragonGiftSent,
            count,
            total,
            (int)dragon,
            (int)gift,
            (int)element
        );
    }

    public void OnDragonBondLevelUp(Dragons dragon, UnitElement element, int count, int total)
    {
        EnqueueEvent(
            MissionCompleteType.DragonBondLevelUp,
            count,
            total,
            (int)dragon,
            (int)element
        );
    }

    public void OnItemSummon()
    {
        EnqueueEvent(MissionCompleteType.ItemSummon);
    }

    public void OnPartyOptimized(UnitElement element)
    {
        EnqueueEvent(MissionCompleteType.PartyOptimized, parameter: (int)element);
    }

    public void OnAbilityCrestTradeViewed()
    {
        EnqueueEvent(MissionCompleteType.AbilityCrestTradeViewed);
    }

    public void OnGuildCheckInRewardClaimed()
    {
        EnqueueEvent(MissionCompleteType.GuildCheckInRewardClaimed);
    }

    public void OnPartyPowerReached(int might)
    {
        EnqueueEvent(MissionCompleteType.PartyPowerReached, 0, might);
    }

    public void OnTreasureTrade(int tradeId, EntityTypes type, int id, int count, int total)
    {
        EnqueueEvent(MissionCompleteType.TreasureTrade, count, total, tradeId, (int)type, id);
    }

    public void EnqueueEvent(
        MissionCompleteType type,
        int value = 1,
        int total = 1,
        int? parameter = null,
        int? parameter2 = null,
        int? parameter3 = null,
        int? parameter4 = null
    )
    {
        eventQueue.Enqueue(
            new MissionEvent(type, value, total, parameter, parameter2, parameter3, parameter4)
        );
    }

    public async Task ProcessMissionEvents()
    {
        if (this.eventQueue.Count == 0)
            return;

        List<DbPlayerMission>? missionList = null;

        logger.LogDebug(
            "Processing mission progression events {@events}",
            this.eventQueue.ToList()
        );

        while (this.eventQueue.TryDequeue(out MissionEvent? evt))
        {
            List<(MissionType Type, int Id)> affectedMissions =
                MasterAsset.MissionProgressionInfo.Enumerable
                    .Where(x => x.CompleteType == evt.Type)
                    .Where(
                        x =>
                            (x.Parameter is null || x.Parameter == evt.Parameter)
                            && (x.Parameter2 is null || x.Parameter2 == evt.Parameter2)
                            && (x.Parameter3 is null || x.Parameter3 == evt.Parameter3)
                            && (x.Parameter4 is null || x.Parameter4 == evt.Parameter4)
                    )
                    .Select(x => (x.MissionType, x.MissionId))
                    .ToList();

            if (affectedMissions.Any())
            {
                missionList ??= await missionRepository.Missions
                    .Where(x => x.State == MissionState.InProgress)
                    .ToListAsync();

                foreach (
                    DbPlayerMission progressingMission in missionList.Where(
                        x =>
                            affectedMissions.Contains((x.Type, x.Id))
                            && x.State == MissionState.InProgress
                    )
                )
                {
                    Mission mission = Mission.From(progressingMission.Type, progressingMission.Id);

                    MissionProgressionInfo progressionInfo =
                        MasterAsset.MissionProgressionInfo.Enumerable.Single(
                            x =>
                                x.MissionType == progressingMission.Type
                                && x.MissionId == progressingMission.Id
                        );

                    if (progressionInfo.UseTotalValue)
                    {
                        if (progressingMission.Progress >= evt.TotalValue)
                            continue;

                        progressingMission.Progress = evt.TotalValue;
                    }
                    else
                    {
                        progressingMission.Progress += evt.Value;
                    }

                    if (progressingMission.Progress >= mission.CompleteValue)
                    {
                        logger.LogDebug(
                            "Completed {missionType} mission {missionId}",
                            progressingMission.Type,
                            progressingMission.Id
                        );
                        progressingMission.State = MissionState.Completed;
                    }
                    else
                    {
                        logger.LogDebug(
                            "Progressed {missionType} mission {missionId} ({currentCount}/{totalCount}",
                            progressingMission.Type,
                            progressingMission.Id,
                            progressingMission.Progress,
                            mission.CompleteValue
                        );
                    }
                }
            }
        }
    }

    private record MissionEvent(
        MissionCompleteType Type,
        int Value,
        int TotalValue,
        int? Parameter = null,
        int? Parameter2 = null,
        int? Parameter3 = null,
        int? Parameter4 = null
    );
}
