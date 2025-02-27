﻿using DragaliaAPI.Database.Entities;
using DragaliaAPI.Features.Login;
using DragaliaAPI.Helpers;
using Microsoft.EntityFrameworkCore;

namespace DragaliaAPI.Features.Dmode;

public class DmodeSkipResetAction(
    IDmodeRepository dmodeRepository,
    IDateTimeProvider dateTimeProvider
) : IDailyResetAction
{
    public async Task Apply()
    {
        DbPlayerDmodeInfo? info = await dmodeRepository.Info.SingleOrDefaultAsync();
        if (info == null)
            return;

        DateTimeOffset time = dateTimeProvider.UtcNow;

        info.FloorSkipCount = 0;
        info.FloorSkipTime = time;
        info.RecoveryCount = 0;
        info.RecoveryTime = time;
    }
}
