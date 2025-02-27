﻿using AutoMapper;
using DragaliaAPI.Database.Repositories;
using DragaliaAPI.Features.Dungeon;
using DragaliaAPI.Features.Dungeon.Record;
using DragaliaAPI.Models.Generated;
using DragaliaAPI.Services;
using DragaliaAPI.Services.Game;
using Microsoft.Extensions.Logging;

namespace DragaliaAPI.Test.Services;

public class HelperServiceTest
{
    private readonly Mock<IPartyRepository> mockPartyRepository;
    private readonly Mock<IDungeonRepository> mockDungeonRepository;
    private readonly Mock<IUserDataRepository> mockUserDataRepository;
    private readonly Mock<ILogger<HelperService>> mockLogger;

    private readonly IHelperService helperService;
    private readonly IMapper mapper;

    public HelperServiceTest()
    {
        this.mapper = new MapperConfiguration(
            cfg => cfg.AddMaps(typeof(Program).Assembly)
        ).CreateMapper();

        this.mockPartyRepository = new(MockBehavior.Strict);
        this.mockDungeonRepository = new(MockBehavior.Strict);
        this.mockUserDataRepository = new(MockBehavior.Strict);
        this.mockLogger = new(MockBehavior.Loose);

        this.helperService = new HelperService(
            this.mockPartyRepository.Object,
            this.mockDungeonRepository.Object,
            this.mockUserDataRepository.Object,
            this.mapper,
            this.mockLogger.Object
        );
    }

    [Fact]
    public void BuildHelperDataContainsCorrectInformationWhenFriended()
    {
        UserSupportList? helperInfo = StubData.HelperList.support_user_list
            .Where(helper => helper.viewer_id == 1000)
            .FirstOrDefault();

        AtgenSupportUserDetailList? helperDetails = StubData.HelperList.support_user_detail_list
            .Where(helper => helper.viewer_id == 1000)
            .FirstOrDefault();

        AtgenSupportData supportData = this.helperService.BuildHelperData(
            helperInfo!,
            helperDetails!
        );

        supportData.viewer_id.Should().Be(1000);
        supportData.name.Should().BeEquivalentTo("Euden");
        supportData.is_friend.Should().Be(true);
        supportData.chara_data.Should().BeEquivalentTo(TestData.supportListEuden.support_chara);
        supportData.dragon_data
            .Should()
            .BeEquivalentTo(
                TestData.supportListEuden.support_dragon,
                o => o.Excluding(x => x.hp).Excluding(x => x.attack)
            );
        supportData.weapon_body_data
            .Should()
            .BeEquivalentTo(TestData.supportListEuden.support_weapon_body);
        supportData.crest_slot_type_1_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListEuden.support_crest_slot_type_1_list);
        supportData.crest_slot_type_2_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListEuden.support_crest_slot_type_2_list);
        supportData.crest_slot_type_3_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListEuden.support_crest_slot_type_3_list);
    }

    [Fact]
    public void BuildHelperDataContainsCorrectInformationWhenNotFriended()
    {
        UserSupportList? helperInfo = StubData.HelperList.support_user_list
            .Where(helper => helper.viewer_id == 1001)
            .FirstOrDefault();

        AtgenSupportUserDetailList? helperDetails = StubData.HelperList.support_user_detail_list
            .Where(helper => helper.viewer_id == 1001)
            .FirstOrDefault();

        AtgenSupportData supportData = this.helperService.BuildHelperData(
            helperInfo!,
            helperDetails!
        );

        supportData.viewer_id.Should().Be(1001);
        supportData.name.Should().BeEquivalentTo("Elisanne");
        supportData.is_friend.Should().Be(false);
        supportData.chara_data.Should().BeEquivalentTo(TestData.supportListElisanne.support_chara);
        supportData.dragon_data
            .Should()
            .BeEquivalentTo(
                TestData.supportListElisanne.support_dragon,
                o => o.Excluding(x => x.hp).Excluding(x => x.attack)
            );
        supportData.weapon_body_data
            .Should()
            .BeEquivalentTo(TestData.supportListElisanne.support_weapon_body);
        supportData.crest_slot_type_1_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListElisanne.support_crest_slot_type_1_list);
        supportData.crest_slot_type_2_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListElisanne.support_crest_slot_type_2_list);
        supportData.crest_slot_type_3_crest_list
            .Should()
            .BeEquivalentTo(TestData.supportListElisanne.support_crest_slot_type_3_list);
    }

    private static class StubData
    {
        public static readonly QuestGetSupportUserListData HelperList =
            new()
            {
                support_user_list = new List<UserSupportList>()
                {
                    TestData.supportListEuden,
                    TestData.supportListElisanne
                },
                support_user_detail_list = new List<AtgenSupportUserDetailList>()
                {
                    new() { viewer_id = 1000, is_friend = true },
                    new() { viewer_id = 1001, is_friend = false }
                }
            };
    }
}
