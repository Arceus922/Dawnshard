﻿using DragaliaAPI.Database;
using DragaliaAPI.Database.Entities;
using DragaliaAPI.Database.Repositories;
using DragaliaAPI.Database.Utils;
using DragaliaAPI.Models.Generated;
using DragaliaAPI.Test.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace DragaliaAPI.Integration.Test.Dragalia;

/// <summary>
/// Tests <see cref="Controllers.Dragalia.TutorialController"/>
/// </summary>
public class TutorialTest : TestFixture
{
    public TutorialTest(CustomWebApplicationFactory factory, ITestOutputHelper outputHelper)
        : base(factory, outputHelper)
    {
        CommonAssertionOptions.ApplyTimeOptions();
    }

    [Fact]
    public async Task TutorialUpdateStep_UpdatesDB()
    {
        int step = 20000;

        TutorialUpdateStepData response = (
            await this.Client.PostMsgpack<TutorialUpdateStepData>(
                "/tutorial/update_step",
                new TutorialUpdateStepRequest(step, 0, 0, 0)
            )
        ).data;

        this.ApiContext.PlayerUserData
            .AsNoTracking()
            .First(x => x.DeviceAccountId == "logged_in_id")
            .TutorialStatus.Should()
            .Be(step);
    }

    [Fact]
    public async Task TutorialUpdateStep_ReturnsCorrectResponse()
    {
        int step = 20000;

        DbPlayerUserData dbUserData = (
            await this.ApiContext.PlayerUserData.FindAsync(DeviceAccountId)
        )!;

        UserData expUserData = this.Mapper.Map<UserData>(dbUserData);
        expUserData.tutorial_status = step;
        UpdateDataList expUpdateData = new() { user_data = expUserData };

        TutorialUpdateStepData response = (
            await this.Client.PostMsgpack<TutorialUpdateStepData>(
                "/tutorial/update_step",
                new TutorialUpdateStepRequest(step, 0, 0, 0)
            )
        ).data;

        response.update_data_list.Should().BeEquivalentTo(expUpdateData);
    }

    [Fact]
    public async Task TutorialUpdateFlags_UpdatesDB()
    {
        int flag = 1020;

        TutorialUpdateFlagsData response = (
            await this.Client.PostMsgpack<TutorialUpdateFlagsData>(
                "/tutorial/update_flags",
                new TutorialUpdateFlagsRequest() { flag_id = flag }
            )
        ).data;

        TutorialFlagUtil
            .ConvertIntToFlagIntList(
                this.ApiContext.PlayerUserData
                    .AsNoTracking()
                    .First(x => x.DeviceAccountId == "logged_in_id")
                    .TutorialFlag
            )
            .Should()
            .BeEquivalentTo(new List<int>() { flag });
    }
}
