using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;

using TownOfHost.Modules;
using TownOfHost.Roles.Impostor;
using TownOfHost.Roles.Crewmate;
using TownOfHost.Roles.Neutral;
using TownOfHost.Roles.AddOns.Impostor;
using TownOfHost.Roles.AddOns.Crewmate;
using static TownOfHost.Translator;
using TownOfHost;

namespace TownOfHost
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    class ChangeRoleSettings
    {
        public static void Postfix(AmongUsClient __instance)
        {
            Logger.Info("The Current Player: " + __instance.ToString(), "Role Assign:");
            foreach (var pc in Main.AllPlayerControls)
            {
                Logger.Info("The given role: " + pc.Data.Role.Role.ToString(), "Role Assign:");
            }
            //注:この時点では役職は設定されていません。
            Main.NormalOptions.roleOptions.SetRoleRate(RoleTypes.GuardianAngel, 0, 0);

            Main.PlayerStates = new();

            Main.AllPlayerKillCooldown = new Dictionary<byte, float>();
            Main.AllPlayerSpeed = new Dictionary<byte, float>();

            Main.WarlockTimer = new Dictionary<byte, float>();
            Main.isDoused = new Dictionary<(byte, byte), bool>();
            Main.ArsonistTimer = new Dictionary<byte, (PlayerControl, float)>();
            Main.CursedPlayers = new Dictionary<byte, PlayerControl>();
            Main.isCurseAndKill = new Dictionary<byte, bool>();
            Main.SKMadmateNowCount = 0;
            Main.isCursed = false;
            Main.PuppeteerList = new Dictionary<byte, byte>();

            Main.AfterMeetingDeathPlayers = new();
            Main.ResetCamPlayerList = new();
            Main.clientIdList = new();

            Main.CheckShapeshift = new();
            Main.ShapeshiftTarget = new();
            Main.SpeedBoostTarget = new Dictionary<byte, byte>();
            Main.MayorUsedButtonCount = new Dictionary<byte, int>();

            ReportDeadBodyPatch.CanReport = new();

            Options.UsedButtonCount = 0;
            Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);

            Main.introDestroyed = false;

            RandomSpawn.CustomNetworkTransformPatch.NumOfTP = new();

            MeetingTimeManager.Init();
            Main.DefaultCrewmateVision = Main.RealOptionsData.GetFloat(FloatOptionNames.CrewLightMod);
            Main.DefaultImpostorVision = Main.RealOptionsData.GetFloat(FloatOptionNames.ImpostorLightMod);

            Main.LastNotifyNames = new();

            Main.currentDousingTarget = 255;
            Main.PlayerColors = new();
            //名前の記録
            Main.AllPlayerNames = new();

            Camouflage.Init();
            var invalidColor = Main.AllPlayerControls.Where(p => p.Data.DefaultOutfit.ColorId < 0 || Palette.PlayerColors.Length <= p.Data.DefaultOutfit.ColorId);
            if (invalidColor.Count() != 0)
            {
                var msg = Translator.GetString("Error.InvalidColor");
                Logger.SendInGame(msg);
                msg += "\n" + string.Join(",", invalidColor.Select(p => $"{p.name}({p.Data.DefaultOutfit.ColorId})"));
                Utils.SendMessage(msg);
                Logger.Error(msg, "CoStartGame");
            }

            foreach (var target in Main.AllPlayerControls)
            {
                foreach (var seer in Main.AllPlayerControls)
                {
                    var pair = (target.PlayerId, seer.PlayerId);
                    Main.LastNotifyNames[pair] = target.name;
                }
            }
            foreach (var pc in Main.AllPlayerControls)
            {
                var colorId = pc.Data.DefaultOutfit.ColorId;
                if (AmongUsClient.Instance.AmHost && Options.ColorNameMode.GetBool()) pc.RpcSetName(Palette.GetColorName(colorId));
                Main.PlayerStates[pc.PlayerId] = new(pc.PlayerId);
                Main.AllPlayerNames[pc.PlayerId] = pc?.Data?.PlayerName;
                Main.PlayerColors[pc.PlayerId] = Palette.PlayerColors[colorId];
                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod); //移動速度をデフォルトの移動速度に変更
                ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
                ReportDeadBodyPatch.WaitReport[pc.PlayerId] = new();
                pc.cosmetics.nameText.text = pc.name;

                RandomSpawn.CustomNetworkTransformPatch.NumOfTP.Add(pc.PlayerId, 0);
                var outfit = pc.Data.DefaultOutfit;
                Camouflage.PlayerSkins[pc.PlayerId] = new GameData.PlayerOutfit().Set(outfit.PlayerName, outfit.ColorId, outfit.HatId, outfit.SkinId, outfit.VisorId, outfit.PetId);
                Main.clientIdList.Add(pc.GetClientId());
            }
            Main.VisibleTasksCount = true;
            if (__instance.AmHost)
            {
                RPC.SyncCustomSettingsRPC();
                Main.RefixCooldownDelay = 0;
                if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                {
                    Options.HideAndSeekKillDelayTimer = Options.KillDelay.GetFloat();
                }
                if (Options.IsStandardHAS)
                {
                    Options.HideAndSeekKillDelayTimer = Options.StandardHASWaitingTime.GetFloat();
                }
            }
            FallFromLadder.Reset();
            BountyHunter.Init();
            SerialKiller.Init();
            FireWorks.Init();
            Sniper.Init();
            TimeThief.Init();
            Mare.Init();
            Witch.Init();
            SabotageMaster.Init();
            Egoist.Init();
            Executioner.Init();
            Jackal.Init();
            Sheriff.Init();
            EvilTracker.Init();
            Snitch.Init();
            SchrodingerCat.Init();
            Vampire.Init();
            TimeManager.Init();
            LastImpostor.Init();
            TargetArrow.Init();
            DoubleTrigger.Init();
            Workhorse.Init();
            CustomWinnerHolder.Reset();
            AntiBlackout.Reset();
            IRandom.SetInstanceById(Options.RoleAssigningAlgorithm.GetValue());

            MeetingStates.MeetingCalled = false;
            MeetingStates.FirstMeeting = true;
            GameStates.AlreadyDied = false;
        }
    }
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    class SelectRolesPatch
    {
        static List<CustomRoles> CrewmateRoles = new(new CustomRoles[] {
            CustomRoles.Jester,
            CustomRoles.Bait,
            CustomRoles.MadGuardian,
            CustomRoles.Opportunist,
            CustomRoles.Snitch,
            CustomRoles.SabotageMaster,
            CustomRoles.SpeedBooster,
            CustomRoles.Lighter,
            CustomRoles.Trapper,
            CustomRoles.Dictator,
            CustomRoles.SchrodingerCat,
            CustomRoles.Seer,
            CustomRoles.Executioner
        });

        static List<CustomRoles> EngineerRoles = new(new CustomRoles[] {
            CustomRoles.Terrorist,
            CustomRoles.Madmate
        });

        static List<CustomRoles> ImpostorRoles = new(new CustomRoles[] {
            CustomRoles.Mafia,
            CustomRoles.Vampire,
            CustomRoles.Witch,
            CustomRoles.Mare,
            CustomRoles.Puppeteer,
            CustomRoles.TimeThief
        });

        static List<CustomRoles> ShapeshifterRoles = new(new CustomRoles[] {
            CustomRoles.FireWorks,
            CustomRoles.Sniper,
            CustomRoles.BountyHunter,
            CustomRoles.Warlock,
            CustomRoles.SerialKiller,
            CustomRoles.EvilTracker,
        });

        static List<CustomRoles> ScientistRoles = new(new CustomRoles[] {
            CustomRoles.Doctor
        });

        static List<CustomRoles> CrewmateRolesDraw = new();
        static List<CustomRoles> EngineerRolesDraw = new();
        static List<CustomRoles> ImpostorRolesDraw = new();
        static List<CustomRoles> ShapeshifterRolesDraw = new();
        static List<CustomRoles> ScientistRolesDraw = new();

        public static void Prefix()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            //CustomRpcSenderとRpcSetRoleReplacerの初期化
            Dictionary<byte, CustomRpcSender> senders = new();
            foreach (var pc in Main.AllPlayerControls)
            {
                senders[pc.PlayerId] = new CustomRpcSender($"{pc.name}'s SetRole Sender", SendOption.Reliable, false)
                        .StartMessage(pc.GetClientId());
            }
            RpcSetRoleReplacer.StartReplace(senders);

            //ウォッチャーの陣営抽選
            Options.SetWatcherTeam(Options.EvilWatcherChance.GetFloat());

            if (Options.CurrentGameMode != CustomGameMode.HideAndSeek)
            {
                RoleTypes[] RoleTypesList = { RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Shapeshifter };
                foreach (var roleTypes in RoleTypesList)
                {
                    var roleOpt = Main.NormalOptions.roleOptions;
                    int additionalNum = GetAdditionalRoleTypesCount(roleTypes);
                    roleOpt.SetRoleRate(roleTypes, roleOpt.GetNumPerGame(roleTypes) + additionalNum, additionalNum > 0 ? 100 : roleOpt.GetChancePerGame(roleTypes));
                }

                List<PlayerControl> AllPlayers = new();
                foreach (var pc in Main.AllPlayerControls)
                {
                    AllPlayers.Add(pc);
                }

                if (Options.EnableGM.GetBool())
                {
                    AllPlayers.RemoveAll(x => x == PlayerControl.LocalPlayer);
                    PlayerControl.LocalPlayer.RpcSetCustomRole(CustomRoles.GM);
                    PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Crewmate);
                    PlayerControl.LocalPlayer.Data.IsDead = true;
                }

                //!!!!!!!!!!!!!!ROLE ASSESSMENT!!!!!!!!!!!!!!!
                /*List<CustomRoles> CrewmateRoles = new(new CustomRoles[] {
                        CustomRoles.Jester,
                        CustomRoles.Bait,
                        CustomRoles.MadGuardian,
                        CustomRoles.Opportunist,
                        CustomRoles.Snitch,
                        CustomRoles.SabotageMaster,
                        CustomRoles.SpeedBooster,
                        CustomRoles.Lighter,
                        CustomRoles.Trapper,
                        CustomRoles.Dictator,
                        CustomRoles.SchrodingerCat,
                        CustomRoles.Seer,
                        CustomRoles.Executioner
                    });*/

                /*List<CustomRoles> EngineerRoles = new(new CustomRoles[] {
                        CustomRoles.Terrorist,
                        CustomRoles.Madmate
                    });*/

                if (Options.MadSnitchCanVent.GetBool())
                    EngineerRoles.Add(CustomRoles.MadSnitch);
                else
                    CrewmateRoles.Add(CustomRoles.MadSnitch);

                if (Options.MayorHasPortableButton.GetBool())
                    EngineerRoles.Add(CustomRoles.Mayor);
                else
                    CrewmateRoles.Add(CustomRoles.Mayor);

                /*List<CustomRoles> ImpostorRoles = new(new CustomRoles[] {
                        CustomRoles.Mafia,
                        CustomRoles.Vampire,
                        CustomRoles.Witch,
                        CustomRoles.Mare,
                        CustomRoles.Puppeteer,
                        CustomRoles.TimeThief
                    });*/

                if (Options.IsEvilWatcher) ImpostorRoles.Add(CustomRoles.Watcher);

                /*List<CustomRoles> ShapeshifterRoles = new(new CustomRoles[] {
                        CustomRoles.FireWorks,
                        CustomRoles.Sniper,
                        CustomRoles.BountyHunter,
                        CustomRoles.Warlock,
                        CustomRoles.SerialKiller,
                        CustomRoles.EvilTracker,
                    });
*/
                /*if (Main.RealOptionsData.NumImpostors > 1)
                    ShapeshifterRoles.Add(CustomRoles.Egoist);
                else
                    CrewmateRoles.Add(CustomRoles.Watcher);*/

                /*List<CustomRoles> ScientistRoles = new(new CustomRoles[] {
                        CustomRoles.Doctor
                    });*/

                //List<CustomRoles> CrewmateRolesDraw = new();
                foreach (CustomRoles role in CrewmateRoles)
                {
                    for (int i = 0; i < role.GetCount(); i++)
                    {
                        Logger.Info(role.ToString() + " amount " + i, "");
                        CrewmateRolesDraw.Add(role);
                    }
                }

                //List<CustomRoles> EngineerRolesDraw = new();
                foreach (CustomRoles role in EngineerRoles)
                {
                    for (int i = 0; i < role.GetCount(); i++)
                    {
                        Logger.Info(role.ToString() + " amount " + i, "");
                        EngineerRolesDraw.Add(role);
                    }
                }

                //List<CustomRoles> ImpostorRolesDraw = new();
                foreach (CustomRoles role in ImpostorRoles)
                {
                    for (int i = 0; i < role.GetCount(); i++)
                    {
                        Logger.Info(role.ToString() + " amount " + i, "");
                        ImpostorRolesDraw.Add(role);
                    }
                }

                //List<CustomRoles> ShapeshifterRolesDraw = new();
                foreach (CustomRoles role in ShapeshifterRoles)
                {
                    for (int i = 0; i < role.GetCount(); i++)
                    {
                        Logger.Info(role.ToString() + " amount " + i, "");
                        ShapeshifterRolesDraw.Add(role);
                    }
                }

                //List<CustomRoles> ScientistRolesDraw = new();
                foreach (CustomRoles role in ScientistRoles)
                {
                    for (int i = 0; i < role.GetCount(); i++)
                    {
                        Logger.Info(role.ToString() + " amount " + i, "");
                        ScientistRolesDraw.Add(role);
                    }
                }


                //!!!!!!!!!!!!!!ROLE ASSESSMENT!!!!!!!!!!!!!!!



                // Uh, gamemode and desync impostor stuff below :P
                var rand = new System.Random();

                Dictionary<(byte, byte), RoleTypes> rolesMap = new();
                List<PlayerControl> AssignedNK = new();
                //List the possible Neutral Killing Roles
                List<CustomRoles> NKRoles = new(new CustomRoles[]
                {
                    CustomRoles.Sheriff,
                    CustomRoles.Jackal,
                    CustomRoles.Arsonist,
                });
                List<CustomRoles> NKRolesDraw = new();

                if (Options.CurrentGameMode == CustomGameMode.BunnyHunt) //gamemode
                {
                    // Figure out how many Jackals there will be
                    int num_NK = (AllPlayers.Count - Main.NormalOptions.NumImpostors);

                    for (int i = 0; i < num_NK; i++)
                    {
                        Logger.Info("Jackal amount " + (i + 1), "");
                        NKRolesDraw.Add(CustomRoles.Jackal);
                    }

                    if (Options.AddSheriff.GetBool())
                    {
                        NKRolesDraw.Remove(CustomRoles.Jackal);
                        NKRolesDraw.Add(CustomRoles.Sheriff);
                    }

                    while (AssignedNK.Count < num_NK)
                    {
                        // Randomly select a player
                        var player = AllPlayers[rand.Next(0, AllPlayers.Count)];
                        AllPlayers.Remove(player);
                        Logger.Info("Chosen Player: " + player.PlayerId, "NK Assign:");

                        var index = rand.Next(0, NKRolesDraw.Count);
                        Logger.Info("NK index: " + index, "");
                        CustomRoles role = NKRolesDraw[index];
                        NKRolesDraw.Remove(role);
                        Logger.Info("Selected NK Role: " + role, "NK Assign:");
                        AssignDesyncRole(role, player, senders, rolesMap, BaseRole: RoleTypes.Impostor);
                        AssignedNK.Add(player);
                    }
                    MakeDesyncSender(senders, rolesMap);
                }
                else if(Options.CurrentGameMode == CustomGameMode.FFA) //gamemode
                {
                    // Figure out how many Jackals there will be
                    int num_NK = (AllPlayers.Count - Main.NormalOptions.NumImpostors);

                    for (int i = 0; i < num_NK; i++)
                    {
                        Logger.Info("Jackal amount " + (i + 1), "");
                        NKRolesDraw.Add(CustomRoles.Jackal);
                    }

                    /*if (Options.AddSheriff.GetBool())
                    {
                        NKRolesDraw.Remove(CustomRoles.Jackal);
                        NKRolesDraw.Add(CustomRoles.Sheriff);
                    }*/

                    while (AssignedNK.Count < num_NK)
                    {
                        // Randomly select a player
                        var player = AllPlayers[rand.Next(0, AllPlayers.Count)];
                        AllPlayers.Remove(player);
                        Logger.Info("Chosen Player: " + player.PlayerId, "NK Assign:");

                        var index = rand.Next(0, NKRolesDraw.Count);
                        Logger.Info("NK index: " + index, "");
                        CustomRoles role = NKRolesDraw[index];
                        NKRolesDraw.Remove(role);
                        Logger.Info("Selected NK Role: " + role, "NK Assign:");
                        AssignDesyncRole(role, player, senders, rolesMap, BaseRole: RoleTypes.Impostor);
                        AssignedNK.Add(player);
                    }
                    MakeDesyncSender(senders, rolesMap);
                }
                else //standard
                {
                    //Find how many of each role is enabled and put them all into a Draw List
                    foreach (CustomRoles role in NKRoles)
                    {
                        for (int i = 0; i < role.GetCount(); i++)
                        {
                            Logger.Info(role.ToString() + " amount " + (i + 1), "");
                            NKRolesDraw.Add(role);
                        }
                    }

                    // Randomly select number of Neutral Killers to assign
                    int num_NK = rand.Next(Options.MinNK.GetInt(), Options.MaxNK.GetInt());
                    Logger.Info("MinNK: " + Options.MinNK.GetInt(), "NK Assign:");
                    Logger.Info("MaxNK: " + Options.MaxNK.GetInt(), "NK Assign:");
                    Logger.Info("num_NK: " + num_NK, "NK Assign:");
                    int Crew = (AllPlayers.Count - Main.NormalOptions.NumImpostors);
                    if (Crew < num_NK) //If there are more NK than players
                    {
                        num_NK = Crew;
                    }
                    Logger.Info("num_NK: " + num_NK, "NK Assign:");
                    if (num_NK > NKRolesDraw.Count) //If the number of NK is greater than the number of enabled NK
                    {
                        num_NK = NKRolesDraw.Count;
                    }
                    Logger.Info("num_NK: " + num_NK, "NK Assign:");

                    while (AssignedNK.Count < num_NK)
                    {
                        // Randomly select a player
                        var player = AllPlayers[rand.Next(0, AllPlayers.Count)];
                        AllPlayers.Remove(player);
                        Logger.Info("Chosen Player: " + player.PlayerId, "NK Assign:");

                        var index = rand.Next(0, NKRolesDraw.Count);
                        Logger.Info("NK index: " + index, "");
                        CustomRoles role = NKRolesDraw[index];
                        NKRolesDraw.Remove(role);
                        Logger.Info("Selected NK Role: " + role, "NK Assign:");
                        AssignDesyncRole(role, player, senders, rolesMap, BaseRole: RoleTypes.Impostor);
                        AssignedNK.Add(player);
                    }

                    /*AssignDesyncRole(CustomRoles.Sheriff, AllPlayers, senders, rolesMap, BaseRole: RoleTypes.Impostor);
                    AssignDesyncRole(CustomRoles.Arsonist, AllPlayers, senders, rolesMap, BaseRole: RoleTypes.Impostor);
                    AssignDesyncRole(CustomRoles.Jackal, AllPlayers, senders, rolesMap, BaseRole: RoleTypes.Impostor);*/
                    MakeDesyncSender(senders, rolesMap);
                }
            }
            //以下、バニラ側の役職割り当てが入る
        }
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost) return;
            RpcSetRoleReplacer.Release(); //保存していたSetRoleRpcを一気に書く
            RpcSetRoleReplacer.senders.Do(kvp => kvp.Value.SendMessage());

            // 不要なオブジェクトの削除
            RpcSetRoleReplacer.senders = null;
            RpcSetRoleReplacer.OverriddenSenderList = null;
            RpcSetRoleReplacer.StoragedData = null;

            //Utils.ApplySuffix();

            var rand = new System.Random();

            List<PlayerControl> Crewmates = new();
            List<PlayerControl> Impostors = new();
            List<PlayerControl> Scientists = new();
            List<PlayerControl> Engineers = new();
            List<PlayerControl> GuardianAngels = new();
            List<PlayerControl> Shapeshifters = new();

            foreach (var pc in Main.AllPlayerControls)
            {
                pc.Data.IsDead = false; //プレイヤーの死を解除する
                if (Main.PlayerStates[pc.PlayerId].MainRole != CustomRoles.NotAssigned) continue; //既にカスタム役職が割り当てられていればスキップ
                var role = CustomRoles.NotAssigned;
                switch (pc.Data.Role.Role)
                {
                    case RoleTypes.Crewmate:
                        Crewmates.Add(pc);
                        role = CustomRoles.Crewmate;
                        break;
                    case RoleTypes.Impostor:
                        Impostors.Add(pc);
                        role = CustomRoles.Impostor;
                        break;
                    case RoleTypes.Scientist:
                        Scientists.Add(pc);
                        role = CustomRoles.Scientist;
                        break;
                    case RoleTypes.Engineer:
                        Engineers.Add(pc);
                        role = CustomRoles.Engineer;
                        break;
                    case RoleTypes.GuardianAngel:
                        GuardianAngels.Add(pc);
                        role = CustomRoles.GuardianAngel;
                        break;
                    case RoleTypes.Shapeshifter:
                        Shapeshifters.Add(pc);
                        role = CustomRoles.Shapeshifter;
                        break;
                    default:
                        Logger.SendInGame(string.Format(GetString("Error.InvalidRoleAssignment"), pc?.Data?.PlayerName));
                        break;
                }
                Main.PlayerStates[pc.PlayerId].SetMainRole(role);
            }
            //gamemode rules below (like how the game ends n' stuff.
            if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
            {
                SetColorPatch.IsAntiGlitchDisabled = true;
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoleTypes.Impostor))
                        pc.RpcSetColor(0);
                    else if (pc.Is(CustomRoleTypes.Crewmate))
                        pc.RpcSetColor(1);
                }

                //役職設定処理
                AssignCustomRolesFromList(CustomRoles.HASFox, Crewmates);
                AssignCustomRolesFromList(CustomRoles.HASTroll, Crewmates);
                foreach (var pair in Main.PlayerStates)
                {
                    //Synchronization via RPC
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);
                }
                //色設定処理
                SetColorPatch.IsAntiGlitchDisabled = true;

                GameEndChecker.SetPredicateToHideAndSeek();
            }
            else if(Options.CurrentGameMode == CustomGameMode.BunnyHunt) //Predicate is what allows the sheriff to win
            {
                GameEndChecker.SetPredicateToBunnyHunt();
            }
            else if (Options.CurrentGameMode == CustomGameMode.FFA) //Predicate is what stops Jackals from winning as a team
            {
                GameEndChecker.SetPredicateToFFA();
            }
            else
            {
                //Assign roles for Standard
                while (Crewmates.Count != 0 && CrewmateRolesDraw.Count != 0)
                {
                    var index = rand.Next(0, CrewmateRolesDraw.Count);
                    Logger.Info("Crewmate index: " + index, "");
                    CustomRoles role = CrewmateRolesDraw[index];
                    CrewmateRolesDraw.Remove(role);
                    AssignCustomRolesFromList(role, Crewmates);
                }

                while (Engineers.Count != 0 && EngineerRolesDraw.Count != 0)
                {
                    var index = rand.Next(0, EngineerRolesDraw.Count);
                    Logger.Info("Engineer index: " + index, "");
                    CustomRoles role = EngineerRolesDraw[index];
                    EngineerRolesDraw.Remove(role);
                    AssignCustomRolesFromList(role, Engineers);
                }

                while (Impostors.Count != 0 && ImpostorRolesDraw.Count != 0)
                {
                    var index = rand.Next(0, ImpostorRolesDraw.Count);
                    Logger.Info("Impostor index: " + index, "");
                    CustomRoles role = ImpostorRolesDraw[index];
                    ImpostorRolesDraw.Remove(role);
                    AssignCustomRolesFromList(role, Impostors);
                }

                while (Shapeshifters.Count != 0 && ShapeshifterRolesDraw.Count != 0)
                {
                    var index = rand.Next(0, ShapeshifterRolesDraw.Count);
                    Logger.Info("Shapeshifter index: " + index, "");
                    CustomRoles role = ShapeshifterRolesDraw[index];
                    ShapeshifterRolesDraw.Remove(role);
                    AssignCustomRolesFromList(role, Shapeshifters);
                }

                while (Scientists.Count != 0 && ScientistRolesDraw.Count != 0)
                {
                    var index = rand.Next(0, ScientistRolesDraw.Count);
                    Logger.Info("Scientist index: " + index, "");
                    CustomRoles role = ScientistRolesDraw[index];
                    ScientistRolesDraw.Remove(role);
                    AssignCustomRolesFromList(role, Scientists);
                }
                AssignLoversRoles();

                //RPCによる同期
                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Is(CustomRoles.Watcher))
                    {
                        Main.PlayerStates[pc.PlayerId].SetMainRole(Options.IsEvilWatcher ? CustomRoles.EvilWatcher : CustomRoles.NiceWatcher);
                    }
                }
                foreach (var pair in Main.PlayerStates)
                {
                    ExtendedPlayerControl.RpcSetCustomRole(pair.Key, pair.Value.MainRole);

                    foreach (var subRole in pair.Value.SubRoles)
                        ExtendedPlayerControl.RpcSetCustomRole(pair.Key, subRole);
                }

                foreach (var pc in Main.AllPlayerControls)
                {
                    if (pc.Data.Role.Role == RoleTypes.Shapeshifter) Main.CheckShapeshift.Add(pc.PlayerId, false);
                    switch (pc.GetCustomRole())
                    {
                        case CustomRoles.BountyHunter:
                            BountyHunter.Add(pc.PlayerId);
                            break;
                        case CustomRoles.SerialKiller:
                            SerialKiller.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Witch:
                            Witch.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Warlock:
                            Main.CursedPlayers.Add(pc.PlayerId, null);
                            Main.isCurseAndKill.Add(pc.PlayerId, false);
                            break;
                        case CustomRoles.FireWorks:
                            FireWorks.Add(pc.PlayerId);
                            break;
                        case CustomRoles.TimeThief:
                            TimeThief.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Sniper:
                            Sniper.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Mare:
                            Mare.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Vampire:
                            Vampire.Add(pc.PlayerId);
                            break;

                        case CustomRoles.Arsonist:
                            foreach (var ar in Main.AllPlayerControls)
                                Main.isDoused.Add((pc.PlayerId, ar.PlayerId), false);
                            break;
                        case CustomRoles.Executioner:
                            Executioner.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Egoist:
                            Egoist.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Jackal:
                            Jackal.Add(pc.PlayerId);
                            break;

                        case CustomRoles.Sheriff:
                            Sheriff.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Mayor:
                            Main.MayorUsedButtonCount[pc.PlayerId] = 0;
                            break;
                        case CustomRoles.SabotageMaster:
                            SabotageMaster.Add(pc.PlayerId);
                            break;
                        case CustomRoles.EvilTracker:
                            EvilTracker.Add(pc.PlayerId);
                            break;
                        case CustomRoles.Snitch:
                            Snitch.Add(pc.PlayerId);
                            break;
                        case CustomRoles.SchrodingerCat:
                            SchrodingerCat.Add(pc.PlayerId);
                            break;
                        case CustomRoles.TimeManager:
                            TimeManager.Add(pc.PlayerId);
                            break;
                    }
                    foreach (var subRole in pc.GetCustomSubRoles())
                    {
                        switch (subRole)
                        {
                            // ここに属性のAddを追加
                            default:
                                break;
                        }
                    }
                    HudManager.Instance.SetHudActive(true);
                    pc.ResetKillCooldown();

                    //通常モードでかくれんぼをする人用
                    if (Options.IsStandardHAS)
                    {
                        foreach (var seer in Main.AllPlayerControls)
                        {
                            if (seer == pc) continue;
                            if (pc.GetCustomRole().IsImpostor() || pc.IsNeutralKiller()) //変更対象がインポスター陣営orキル可能な第三陣営
                                NameColorManager.Add(seer.PlayerId, pc.PlayerId);
                        }
                    }
                }

                RoleTypes[] RoleTypesList = { RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.Shapeshifter };
                foreach (var roleTypes in RoleTypesList)
                {
                    var roleOpt = Main.NormalOptions.roleOptions;
                    roleOpt.SetRoleRate(roleTypes, roleOpt.GetNumPerGame(roleTypes) - GetAdditionalRoleTypesCount(roleTypes), roleOpt.GetChancePerGame(roleTypes));
                }
                GameEndChecker.SetPredicateToNormal();

                GameOptionsSender.AllSenders.Clear();
                foreach (var pc in Main.AllPlayerControls)
                {
                    GameOptionsSender.AllSenders.Add(
                        new PlayerGameOptionsSender(pc)
                    );
                }
            }

            // ResetCamが必要なプレイヤーのリストにクラス化が済んでいない役職のプレイヤーを追加
            Main.ResetCamPlayerList.AddRange(Main.AllPlayerControls.Where(p => p.GetCustomRole() is CustomRoles.Arsonist).Select(p => p.PlayerId));
            /*
            //インポスターのゴーストロールがクルーになるバグ対策
            foreach (var pc in PlayerControl.AllPlayerControls)
            {
                if (pc.Data.Role.IsImpostor || Main.ResetCamPlayerList.Contains(pc.PlayerId))
                {
                    pc.Data.Role.DefaultGhostRole = RoleTypes.ImpostorGhost;
                }
            }
            */
            Utils.CountAlivePlayers(true);
            Utils.SyncAllSettings();
            SetColorPatch.IsAntiGlitchDisabled = false;
        }
        private static void AssignDesyncRole(CustomRoles role, PlayerControl player, Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap, RoleTypes BaseRole, RoleTypes hostBaseRole = RoleTypes.Crewmate)
        {
            Logger.Info("Enter AssignDesyncRole", "NK Assign:");
            if (!role.IsEnable()) return;

            var hostId = PlayerControl.LocalPlayer.PlayerId;
            var rand = new Random();

            for (var i = 0; i < role.GetCount(); i++)
            {
                Main.PlayerStates[player.PlayerId].SetMainRole(role);

                var selfRole = player.PlayerId == hostId ? hostBaseRole : BaseRole;
                var othersRole = player.PlayerId == hostId ? RoleTypes.Crewmate : RoleTypes.Scientist;

                //Desync役職視点
                foreach (var target in Main.AllPlayerControls)
                {
                    if (player.PlayerId != target.PlayerId)
                    {
                        rolesMap[(player.PlayerId, target.PlayerId)] = othersRole;
                    }
                    else
                    {
                        rolesMap[(player.PlayerId, target.PlayerId)] = selfRole;
                    }
                }

                //他者視点
                foreach (var seer in Main.AllPlayerControls)
                {
                    if (player.PlayerId != seer.PlayerId)
                    {
                        rolesMap[(seer.PlayerId, player.PlayerId)] = othersRole;
                    }
                }
                RpcSetRoleReplacer.OverriddenSenderList.Add(senders[player.PlayerId]);
                //ホスト視点はロール決定
                player.SetRole(othersRole);
                player.Data.IsDead = true;
            }
        }
        public static void MakeDesyncSender(Dictionary<byte, CustomRpcSender> senders, Dictionary<(byte, byte), RoleTypes> rolesMap)
        {
            var hostId = PlayerControl.LocalPlayer.PlayerId;
            foreach (var seer in Main.AllPlayerControls)
            {
                var sender = senders[seer.PlayerId];
                foreach (var target in Main.AllPlayerControls)
                {
                    if (rolesMap.TryGetValue((seer.PlayerId, target.PlayerId), out var role))
                    {
                        sender.RpcSetRole(seer, role, target.GetClientId());
                    }
                }
            }
        }

        private static List<PlayerControl> AssignCustomRolesFromList(CustomRoles role, List<PlayerControl> players, int RawCount = -1)
        {
            if (players == null || players.Count <= 0) return null;
            var rand = IRandom.Instance;
            var count = Math.Clamp(RawCount, 0, players.Count);
            if (RawCount == -1) count = Math.Clamp(role.GetCount(), 0, players.Count);
            if (count <= 0) return null;
            List<PlayerControl> AssignedPlayers = new();
            SetColorPatch.IsAntiGlitchDisabled = true;
            for (var i = 0; i < count; i++)
            {
                var player = players[rand.Next(0, players.Count)];
                AssignedPlayers.Add(player);
                players.Remove(player);
                Main.PlayerStates[player.PlayerId].SetMainRole(role);
                Logger.Info("役職設定:" + player?.Data?.PlayerName + " = " + role.ToString(), "AssignRoles");

                if (Options.CurrentGameMode == CustomGameMode.HideAndSeek)
                {
                    if (player.Is(CustomRoles.HASTroll))
                        player.RpcSetColor(2);
                    else if (player.Is(CustomRoles.HASFox))
                        player.RpcSetColor(3);
                }
            }
            SetColorPatch.IsAntiGlitchDisabled = false;
            return AssignedPlayers;
        }

        private static void AssignCustomSubRolesFromList(CustomRoles role, int RawCount = -1)
        {
            if (!role.IsEnable()) return;
            var allPlayers = new List<PlayerControl>();
            foreach (var pc in Main.AllPlayerControls)
                if (IsAssignTarget(pc, role))
                    allPlayers.Add(pc);

            if (RawCount == -1) RawCount = role.GetCount();
            int count = Math.Clamp(RawCount, 0, allPlayers.Count);
            if (count <= 0) return;

            var rand = IRandom.Instance;
            for (var i = 0; i < count; i++)
            {
                var player = allPlayers[rand.Next(allPlayers.Count)];
                allPlayers.Remove(player);
                Main.PlayerStates[player.PlayerId].SetSubRole(role);
                Logger.Info("役職設定:" + player?.Data?.PlayerName + " = " + player.GetCustomRole().ToString() + " + " + role.ToString(), "AssignCustomSubRoles");
            }
        }
        //属性ごとの割り当て条件
        private static bool IsAssignTarget(PlayerControl player, CustomRoles subrole)
        {
            if (player.Is(CustomRoles.GM)) return false;
            return subrole switch
            {
                _ => true,
            };
        }
        private static void AssignLoversRoles(int RawCount = -1)
        {
            if (!CustomRoles.Lovers.IsEnable()) return;
            //Loversを初期化
            Main.LoversPlayers.Clear();
            Main.isLoversDead = false;
            var allPlayers = new List<PlayerControl>();
            foreach (var player in Main.AllPlayerControls)
            {
                if (player.Is(CustomRoles.GM)) continue;
                allPlayers.Add(player);
            }
            var loversRole = CustomRoles.Lovers;
            var rand = IRandom.Instance;
            var count = Math.Clamp(RawCount, 0, allPlayers.Count);
            if (RawCount == -1) count = Math.Clamp(loversRole.GetCount(), 0, allPlayers.Count);
            if (count <= 0) return;

            for (var i = 0; i < count; i++)
            {
                var player = allPlayers[rand.Next(0, allPlayers.Count)];
                Main.LoversPlayers.Add(player);
                allPlayers.Remove(player);
                Main.PlayerStates[player.PlayerId].SetSubRole(loversRole);
                Logger.Info("役職設定:" + player?.Data?.PlayerName + " = " + player.GetCustomRole().ToString() + " + " + loversRole.ToString(), "AssignLovers");
            }
            RPC.SyncLoversPlayers();
        }
        public static int GetAdditionalRoleTypesCount(RoleTypes roleTypes)
        {
            int count = 0;
            foreach (var role in Enum.GetValues(typeof(CustomRoles)).Cast<CustomRoles>().Where(x => x < CustomRoles.NotAssigned))
            {
                if (role.IsVanilla()) continue;
                if (role is CustomRoles.Sheriff or CustomRoles.Arsonist or CustomRoles.Jackal) continue;
                if (role == CustomRoles.Egoist && Main.NormalOptions.GetInt(Int32OptionNames.NumImpostors) <= 1) continue;
                if (role.GetRoleTypes() == roleTypes)
                    count += role.GetCount();
            }
            return count;
        }
        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
        class RpcSetRoleReplacer
        {
            public static bool doReplace = false;
            public static Dictionary<byte, CustomRpcSender> senders;
            public static List<(PlayerControl, RoleTypes)> StoragedData = new();
            // 役職Desyncなど別の処理でSetRoleRpcを書き込み済みなため、追加の書き込みが不要なSenderのリスト
            public static List<CustomRpcSender> OverriddenSenderList;
            public static List<PlayerControl> Scientist = new();
            public static List<PlayerControl> Engineers = new();
            public static List<PlayerControl> SS = new();
            public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] RoleTypes roleType)
            {
                if (doReplace && senders != null)
                {
                    var rand4 = new Random();
                    int draw = rand4.Next(1, 3);
                    string l = draw.ToString();
                    Logger.Info("The Random number is: " + l, "Role Assign:");
                    if (roleType is RoleTypes.Impostor or RoleTypes.Shapeshifter && SS.Count < ShapeshifterRolesDraw.Count)
                    {
                        switch (draw)
                        {
                            case 1:
                                Logger.Info("Enter impostor: " + __instance.ToString(), "Role Assign:");
                                StoragedData.Add((__instance, RoleTypes.Impostor));
                                break;
                            case 2:
                                Logger.Info("Enter Shapeshifter: " + __instance, "Role Assign:");
                                StoragedData.Add((__instance, RoleTypes.Shapeshifter));
                                SS.Add(__instance);
                                break;
                        }
                    }
                    /*if (roleType is RoleTypes.Crewmate or RoleTypes.Engineer or RoleTypes.Scientist)
                    {
                        if(CustomRoles.Terrorist.GetCount() == 0 || CustomRoles.Madmate.GetCount() == 0 && roleType is RoleTypes.Engineer)
                        {
                            StoragedData.Add((__instance, RoleTypes.Crewmate));
                        }
                        if(CustomRoles.Doctor.GetCount() == 0 && roleType is RoleTypes.Scientist)
                        {
                            StoragedData.Add((__instance, RoleTypes.Crewmate));
                        }
                        if (roleType is RoleTypes.Scientist)
                        {
                            Scientist.Add(__instance);
                            if (Scientist.Count() < 1)
                            {
                                StoragedData.Add((__instance, RoleTypes.Scientist));
                            }
                            else
                            {
                                StoragedData.Add((__instance, RoleTypes.Crewmate));
                            }
                        }
                        Logger.Info("The Current Player: " + __instance.ToString(), "Role Assign:");
                        Logger.Info("The given role: " + roleType.ToString(), "Role Assign:");
                        StoragedData.Add((__instance, roleType));
                    }*/
                    Logger.Info("The Current Player: " + __instance.ToString(), "Role Assign:");
                    Logger.Info("The given role: " + roleType.ToString(), "Role Assign:");
                    StoragedData.Add((__instance, roleType));
                    return false;
                }
                else return true;
            }
            public static void Release()
            {
                foreach (var sender in senders)
                {
                    if (OverriddenSenderList.Contains(sender.Value)) continue;
                    if (sender.Value.CurrentState != CustomRpcSender.State.InRootMessage)
                        throw new InvalidOperationException("A CustomRpcSender had Invalid State.");

                    foreach (var pair in StoragedData)
                    {
                        pair.Item1.SetRole(pair.Item2);
                        sender.Value.AutoStartRpc(pair.Item1.NetId, (byte)RpcCalls.SetRole, Utils.GetPlayerById(sender.Key).GetClientId())
                            .Write((ushort)pair.Item2)
                            .EndRpc();
                    }
                    sender.Value.EndMessage();
                }
                doReplace = false;
            }
            public static void StartReplace(Dictionary<byte, CustomRpcSender> senders)
            {
                RpcSetRoleReplacer.senders = senders;
                StoragedData = new();
                OverriddenSenderList = new();
                doReplace = true;
            }
        }
    }
}