﻿using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Tick
{
    #region RawData
    public DemoCommand command;
    public int tickNumber;
    public byte playerSlot;
    public uint size;
    public int messageType;
    #endregion

    public DemoParser demo { get; private set; }

    public Tick(DemoParser demo)
    {
        this.demo = demo;
    }

    public void ParseTickData(byte[] data)
    {
        if (command == DemoCommand.DataTables)
        {
            ParseDataTables(data);
        }
        else if (command == DemoCommand.Signon || command == DemoCommand.Packet)
        {
            uint currentIndex = 0, bytesRead = 0;
            while (currentIndex < data.Length)
            {
                messageType = 0;
                uint length = 0;
                if (data != null)
                {
                    messageType = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                    length = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                }
                //Debug.Log("Number: " + tickNumber + " Command: " + command + "\nMessageType: " + (messageType >= 8 ? ((SVC_Messages)messageType).ToString() : ((NET_Messages)messageType).ToString()) + " MessageLength: " + length);
                //if (DemoParser.prints < 500) { Debug.Log((SVC_Messages)messageType); DemoParser.prints++; }

                byte[] cmdBytes = DataParser.ReadBytes(data, currentIndex, length);
                if ((SVC_Messages)messageType == SVC_Messages.svc_PacketEntities)
                {
                    ParsePacketEntities(cmdBytes);
                }
                else if ((SVC_Messages)messageType == SVC_Messages.svc_GameEventList)
                {
                    ParseGameEventList(cmdBytes);
                }
                else if ((SVC_Messages)messageType == SVC_Messages.svc_GameEvent)
                {
                    ParseGameEvent(cmdBytes);
                }
                else if ((SVC_Messages)messageType == SVC_Messages.svc_CreateStringTable)
                {
                    ParseCreateStringTable(cmdBytes);
                }
                else if ((SVC_Messages)messageType == SVC_Messages.svc_UpdateStringTable)
                {
                    ParseUpdateStringTable(cmdBytes);
                }
                else if ((NET_Messages)messageType == NET_Messages.net_Tick)
                {
                    ParseNetTick(cmdBytes);
                }
                currentIndex += length;
            }
        }
    }

    #region Message Parsers
    private void ParseDataTables(byte[] data)
    {
        demo.dataTables = DataTables.Parse(data);

        MapEquipment(demo.dataTables);

        BindEntities(demo.dataTables);
    }
    private void MapEquipment(DataTables dataTables)
    {
        for (int i = 0; i < dataTables.serverClasses.Length; i++)
        {
            ServerClass sc = dataTables.serverClasses[i];

            if (sc.baseClasses.Length > 6 && sc.baseClasses[6].name == "CWeaponCSBase")
            {
                try { demo.equipmentMapping.Add(sc, (EquipmentElement)Enum.Parse(typeof(EquipmentElement), sc.dataTableName.Substring(3))); }
                catch (Exception e)
                {
                    //Debug.Log(e.ToString());
                }
            }
        }
    }
    private void BindEntities(DataTables dataTables)
    {
        //dataTables.FindByName("CCSTeam")
    }
    private void ParsePacketEntities(byte[] data)
    {
        PacketEntities.Parse(data, this);
        #region Debug
        //if(DemoParser.prints == 50000)
        //{
        //    string debugString = "";
        //    foreach (KeyValuePair<int, Entity> entity in demo.entities)
        //    {
                //if (entity != null)
                //{
        //            debugString += "id: " + entity.Value.id + "==" + entity.Key + ", name: " + entity.Value.serverClass.name + ", dtName: " + entity.Value.serverClass.dataTableName + "\nProperties\n";
        //            foreach (Entity.PropertyEntry propertyEntry in entity.Value.properties)
        //            {
        //                debugString += propertyEntry.entry.propertyName + ": " + propertyEntry.value + "\n";
        //            }
        //            debugString += "\n";
                //}
        //    }
        //    Debug.Log(debugString);
        //}
        DemoParser.prints++;
        #endregion
    }
    private void ParseGameEventList(byte[] data)
    {
        demo.gameEventDescriptors = new Dictionary<int, EventDescriptor>();

        uint currentIndex = 0;
        uint bytesRead = 0;

        while (currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType != 2 || fieldNum != 1) throw new Exception("GameEventList: WireType must equal 2 and FieldNum must equal 1");

            uint descriptorLength = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            EventDescriptor descriptor = EventDescriptor.Parse(DataParser.ReadBytes(data, currentIndex, descriptorLength));
            demo.gameEventDescriptors.Add(descriptor.eventID, descriptor);
            currentIndex += descriptorLength;
        }

        //for(int i = 0; i < demo.gameEventDescriptors.Count; i++)
        //{
        //    string eventDebug = demo.gameEventDescriptors.Keys.ElementAt(i) + ": " + demo.gameEventDescriptors.Values.ElementAt(i).name + "\n";
        //    for(int j = 0; j < demo.gameEventDescriptors.Values.ElementAt(i).keys.Length; j++)
        //    {
        //        eventDebug += "\n" + demo.gameEventDescriptors.Values.ElementAt(i).keys[j].name;
        //    }
        //    Debug.Log(eventDebug);
        //}
    }
    private void ParseGameEvent(byte[] data)
    {
        GameEvent gameEvent = GameEvent.Parse(data);

        if (demo.gameEventDescriptors == null) return;
        EventDescriptor eventDescriptor = demo.gameEventDescriptors[gameEvent.eventID];
        //if (Players.Count <= 0 && eventDescriptor.name != "player_connect") return;

        Dictionary<string, object> eventData = GameEvent.MapData(eventDescriptor, gameEvent);

        //if (!demo.uniqueEvents.ContainsKey(eventDescriptor)) demo.uniqueEvents.Add(eventDescriptor, eventData); //Debug Unique Events

        #region Event Handling
        //Used Events in BlazingBlace: 163 21 9 27 229 125 11 151 153 24 23 164 129 234 154 128 152 165 181 148 107 36 149 106 50 13 14 168 40 139 190 178 42 66 44 157 156 101 15 103 124 126 158 159 112 105 7 216 166 39 110 37 179 92
        #region Events used in recent Replay
        //player_footstep (int userid = 6)
        //weapon_fire (int userid = 3, string weapon = weapon_m4a1, bool silenced = False)
        //player_hurt (int userid = 12, int attacker = 3, int health = 68, int armor = 0, string weapon = m4a1, int dmg_health = 32, int dmg_armor = 0, int hitgroup = 2)
        //player_death (int userid = 12, int attacker = 3, int assister = 0, string weapon = m4a1, int weapon_itemid = 0, int weapon_fauxitemid = 18446744069414584336, int weapon_originalowner_xuid = 76561198122821247, bool headshot = True, int dominated = 0, int revenge = 0, int penetrated = 0, bool noreplay = False)
        //weapon_zoom (int userid = 10)
        //player_jump (int userid = 3)
        //hltv_status (int clients = 0, int slots = 50, int proxies = 0, ? master = , int externaltotal = 0, int externallinked = 0)
        //player_spawn (int userid = 12, int teamnum = 3)
        //hltv_chase (int target1 = 2, int target2 = 11, int distance = 96, int theta = -30, int phi = -20, int inertia = 0, int ineye = 1)
        //weapon_reload (int userid = 12)
        //weapon_fire_on_empty (int userid = 12, string weapon = weapon_m4a1)
        //player_team (int userid = 8, int team = 3, int oldteam = 0, bool disconnect = False, bool autoteam = False, bool silent = False, bool isbot = False)
        //player_disconnect (int userid = 20, string reason = Kicked by Console, string name Yogi, string networkid BOT)
        //decoy_started (int userid = 12, int entityid = 480, float x = 775.8956, float y = 2075.691, float z = 193.1547)
        //decoy_detonate (int userid = 12, int entityid = 480, float x = 775.8956, float y = 2075.691, float z = 193.1547)
        //cs_pre_restart ()
        //round_prestart ()
        //bomb_pickup (int userid = 7)
        //round_start (int timelimit = 115, int fraglimit = 0, string objective = BOMB TARGET)
        //round_poststart ()
        //begin_new_match ()
        //cs_round_start_beep ()
        //cs_round_final_beep ()
        //round_freeze_end ()
        //round_announce_match_start ()
        //other_death (int otherid = 97, string othertype = chicken, int attacker = 5, string weapon = knife_t, int weapon_itemid = 0, int weapon_fauxitemid = 18446744069414584379, int weapon_originalowner_xuid = 76561198138395904, bool headshot = False, int penetrated = 0)
        //flashbang_detonate (int userid = 10, int entityid = 112, float x = 116.3785, float y = 1348.125, float z = 125.1487)
        //player_blind (int userid = 5)
        //buytime_ended ()
        //bomb_dropped (int userid = 7, int entindex = 610)
        //round_mvp (int userid = 4, int reason = 1, int musickkitmvps = 0)
        //cs_win_panel_round (bool show_timer_defend = False, bool show_timer_attack = True, int timer_time = 41, int final_event = 8, string funfact_token = #funfact_kills_headshots, int funfact_player = 3, int funfact_data1 = 4, int funfact_data2 = 0, int funfact_data3 = 0)
        //round_end (int winner = 3, int reason = 8, string message = #SFUI_Notice_CTs_Win, int legacy = 0, int player_count = 0)
        //round_officially_ended ()
        //hegrenade_detonate (int userid = 4, int entityid = 140, float x = 82.71462, float y = 1288.01, float z = 108.7554)
        //smokegrenade_detonate (int userid = 10, int entityid = 121, float x = 124.7404, float y = 1538.201, float z = 104.3713)
        //smokegrenade_expired (int userid = 10, int entityid = 121, float x = 124.7404, float y = 1538.201, float z = 104.3713)
        //player_falldamage (int userid = 3, float damage = 5.775379)
        //bomb_beginplant (int userid = 5, int site = 380)
        //bomb_planted (int userid = 5, int site = 380)
        //bomb_beep (int entindex = 521)
        //inferno_startburn (int entityid = 109, float x = 716.717, float y = 1924.841, float z = 194.4628)
        //inferno_expire (int entityid = 109, float x = 716.717, float y = 1924.841, float z = 194.4628)
        //round_time_warning ()
        //player_connect (string name = Lester, int index = 9, int userid = 22, string networkid = BOT, ? address = )
        //round_announce_last_round_half ()
        //bot_takeover (int userid = 7, int botid = 22, int index = 5)
        //announce_phase_end ()
        //bomb_exploded (int userid = 3, int site = 414)
        //bomb_begindefuse (int userid = 3, bool haskit = False)
        //bomb_defused (int userid = 3, int site = 380)
        //round_announce_final ()
        //cs_win_panel_match ()
        //endmatch_cmm_start_reveal_items ()
        #endregion
        if (eventDescriptor.name == "achievement_earned") ; //66 player achievement
        else if (eventDescriptor.name == "achievement_earned_local") ; //186 achievement splitscreenplayer
        else if (eventDescriptor.name == "achievement_event") ; //64 achievement_name cur_val max_val
        else if (eventDescriptor.name == "achievement_increment") ; //65 achievement_id cur_val max_val
        else if (eventDescriptor.name == "achievement_info_loaded") ; //173
        else if (eventDescriptor.name == "achievement_write_failed") ; //67
        else if (eventDescriptor.name == "add_bullet_hit_marker") ; //n101 userid bone pos_x pos_y pos_z ang_x ang_y ang_z start_x start_y start_z hit
        else if (eventDescriptor.name == "add_player_sonar_icon") ; //n100 userid pos_x pos_y pos_z
        else if (eventDescriptor.name == "ammo_pickup") ; //135 userid item index
        else if (eventDescriptor.name == "announce_phase_end") ; //110
        else if (eventDescriptor.name == "assassination_target_killed") ; //194 target killer
        else if (eventDescriptor.name == "begin_new_match") ; //50
        else if (eventDescriptor.name == "bomb_abortdefuse") ; //113 userid
        else if (eventDescriptor.name == "bomb_abortplant") ; //102 userid site
        else if (eventDescriptor.name == "bomb_beep") ; //124 entindex
        else if (eventDescriptor.name == "bomb_begindefuse") ; //112 userid haskit
        else if (eventDescriptor.name == "bomb_beginplant") ; //101 userid site
        else if (eventDescriptor.name == "bomb_defused") ; //104 userid site
        else if (eventDescriptor.name == "bomb_dropped") ; //106 userid entindex
        else if (eventDescriptor.name == "bomb_exploded") ; //105 userid site
        else if (eventDescriptor.name == "bomb_pickup") ; //107 userid
        else if (eventDescriptor.name == "bomb_planted") ; //103 userid site
        else if (eventDescriptor.name == "bonus_updated") ; //62 numadvanced numbronze numsilver numgold
        else if (eventDescriptor.name == "bot_takeover") ; //216 userid botid index
        else if (eventDescriptor.name == "break_breakable") ; //58 entindex userid material
        else if (eventDescriptor.name == "break_prop") ; //59 entindex userid
        else if (eventDescriptor.name == "bullet_impact") ; //162 userid x y z
        else if (eventDescriptor.name == "buymenu_close") ; //147 userid
        else if (eventDescriptor.name == "buymenu_open") ; //146 userid
        else if (eventDescriptor.name == "buytime_ended") ; //139
        else if (eventDescriptor.name == "cart_updated") ; //94
        else if (eventDescriptor.name == "client_disconnect") ; //191
        else if (eventDescriptor.name == "client_loadout_changed") ; //98
        else if (eventDescriptor.name == "cs_game_disconnected") ; //177
        else if (eventDescriptor.name == "cs_intermission") ; //111
        else if (eventDescriptor.name == "cs_match_end_restart") ; //180
        else if (eventDescriptor.name == "cs_pre_restart") ; //181
        else if (eventDescriptor.name == "cs_prev_next_spectator") ; //223 next
        else if (eventDescriptor.name == "cs_round_start_beep") ; //13
        else if (eventDescriptor.name == "cs_round_final_beep") ; //14
        else if (eventDescriptor.name == "cs_win_panel_match") ; //179 show_time_defend show_timer_attack timer_time final_event funfact_token funfact_player funfact_data1 funfact_data2 funfact_data3
        else if (eventDescriptor.name == "cs_win_panel_round") ; //178 show_timer_defend show_timer_attack timer_time final_event funfact_token funfact_player funfact_data1 funfact_data2 funfact_data3
        else if (eventDescriptor.name == "decoy_detonate") ; //156 userid entityid x y z
        else if (eventDescriptor.name == "decoy_firing") ; //161 userid entityid x y z
        else if (eventDescriptor.name == "decoy_started") ; //157 userid entityid x y z
        else if (eventDescriptor.name == "defuser_dropped") ; //108 entityid
        else if (eventDescriptor.name == "defuser_pickup") ; //109 entityid userid
        else if (eventDescriptor.name == "difficulty_changed") ; //54 newDifficulty oldDifficulty strDifficulty
        else if (eventDescriptor.name == "dm_bonus_weapon_start") ; //57 time wepID Pos
        else if (eventDescriptor.name == "door_moving") ; //167 entindex userid
        else if (eventDescriptor.name == "enable_restart_voting") ; //207 enable
        else if (eventDescriptor.name == "endmatch_cmm_start_reveal_items") ; //92
        else if (eventDescriptor.name == "endmatch_mapvote_selecting_map") ; //91 count slot1 slot2 slot3 slot4 slot5 slot6 slot7 slot8 slot9 slot10
        else if (eventDescriptor.name == "enter_bombzone") ; //140 userid hasbomb isplanted
        else if (eventDescriptor.name == "enter_buyzone") ; //137 userid canbuy
        else if (eventDescriptor.name == "enter_rescue_zone") ; //142 userid
        else if (eventDescriptor.name == "entity_killed") ; //61 entindex_killed entindex_attacker entindex_inflictor damagebits
        else if (eventDescriptor.name == "entity_visible") ; //76 userid subject classname entityname
        else if (eventDescriptor.name == "exit_bombzone") ; //141 userid hasbomb isplanted
        else if (eventDescriptor.name == "exit_buyzone") ; //138 userid canbuy
        else if (eventDescriptor.name == "exit_rescue_zone") ; //143 userid
        else if (eventDescriptor.name == "finale_start") ; //55 rushes
        else if (eventDescriptor.name == "flare_ignite_npc") ; //69 entindex
        else if (eventDescriptor.name == "flashbang_detonate") ; //152 userid entityid x y z
        else if (eventDescriptor.name == "freezecam_started") ; //184
        else if (eventDescriptor.name == "game_end") ; //35 winner
        else if (eventDescriptor.name == "game_init") ; //32
        else if (eventDescriptor.name == "game_messages") ; //56 target text
        else if (eventDescriptor.name == "game_newmap") ; //33 mapname
        else if (eventDescriptor.name == "game_start") ; //34 roundslimit timelimit fraglimit objective
        else if (eventDescriptor.name == "gameinstructor_draw") ; //73
        else if (eventDescriptor.name == "gameinstructor_nodraw") ; //74
        else if (eventDescriptor.name == "gameui_hidden") ; //19
        else if (eventDescriptor.name == "gc_connected") ; //96
        else if (eventDescriptor.name == "gg_bonus_grenade_achieved") ; //198 userid
        else if (eventDescriptor.name == "gg_final_weapon_achieved") ; //197 playerid
        else if (eventDescriptor.name == "gg_killed_enemy") ; //196 victimid attackerid dominated revenge bonus
        else if (eventDescriptor.name == "gg_leader") ; //200 playerid
        else if (eventDescriptor.name == "gg_player_impending_upgrade") ; //202 userid
        else if (eventDescriptor.name == "gg_player_levelup") ; //192 userid weaponrank weaponname
        else if (eventDescriptor.name == "gg_reset_round_start_sounds") ; //211 userid
        else if (eventDescriptor.name == "gg_team_leader") ; //201 playerid
        else if (eventDescriptor.name == "ggprogressive_player_levelup") ; //195 userid weaponrank weaponname
        else if (eventDescriptor.name == "ggtr_player_levelup") ; //193 userid weaponrank weaponname
        else if (eventDescriptor.name == "grenade_bounce") ; //150 userid
        else if (eventDescriptor.name == "hegrenade_detonate") ; //151 userid entityid x y z
        else if (eventDescriptor.name == "helicopter_grenade_punt_miss") ; //70
        else if (eventDescriptor.name == "hide_freezepanel") ; //183
        else if (eventDescriptor.name == "hltv_cameraman") ; //230 index
        else if (eventDescriptor.name == "hltv_changed_mode") ; //176 oldmode newmode obs_target
        else if (eventDescriptor.name == "hltv_changed_target") ; //238 mode old_target obs_target
        else if (eventDescriptor.name == "hltv_chase") ; //234 target1 target2 distance theta phi inertia ineye
        else if (eventDescriptor.name == "hltv_chat") ; //237 text
        else if (eventDescriptor.name == "hltv_fixed") ; //233 posx posy posz theta phi offset fov target
        else if (eventDescriptor.name == "hltv_message") ; //235 text
        else if (eventDescriptor.name == "hltv_rank_camera") ; //231 index rank target
        else if (eventDescriptor.name == "hltv_rank_entity") ; //232 index rank target
        else if (eventDescriptor.name == "hltv_status") ; //229 clients slots proxies master externaltotal externallinked
        else if (eventDescriptor.name == "hltv_title") ; //236 text
        else if (eventDescriptor.name == "hostage_call_for_help") ; //120 hostage
        else if (eventDescriptor.name == "hostage_follows") ; //114 userid hostage
        else if (eventDescriptor.name == "hostage_hurt") ; //115 userid hostage
        else if (eventDescriptor.name == "hostage_killed") ; //116 userid hostage
        else if (eventDescriptor.name == "hostage_rescued") ; //117 userid hostage site
        else if (eventDescriptor.name == "hostage_rescued_all") ; //119
        else if (eventDescriptor.name == "hostage_stops_following") ; //118 userid hostage
        else if (eventDescriptor.name == "hostname_changed") ; //53 hostname
        else if (eventDescriptor.name == "inferno_expire") ; //159 entityid x y z
        else if (eventDescriptor.name == "inferno_extinguish") ; //160 entityid x y z
        else if (eventDescriptor.name == "inferno_startburn") ; //158 entityid x y z
        else if (eventDescriptor.name == "inspect_weapon") ; //131 userid
        else if (eventDescriptor.name == "instructor_server_hint_create") ; //78 hint_name hint_replace_key hint_target hint_activator_userid hint_timeout hint_icon_onscreen hint_icon_offscreen hint_caption hint_activator_caption hint_color hint_icon_offset hint_range hint_flags hint_binding hint_gamepad_binding hint_allow_nodraw hint_nooffscreen hint_forcecaption hint_local_player_only
        else if (eventDescriptor.name == "instructor_server_hint_stop") ; //79 hint_name
        else if (eventDescriptor.name == "inventory_updated") ; //93
        else if (eventDescriptor.name == "item_equip") ; //136 userid item canzoom hassilencer issilenced hastracers weptype ispainted
        else if (eventDescriptor.name == "item_found") ; //187 player quality method itemdef itemid
        else if (eventDescriptor.name == "item_pickup") ; //134 userid item silent
        else if (eventDescriptor.name == "item_purchase") ; //100 userid team weapon
        else if (eventDescriptor.name == "item_schema_initialized") ; //97
        else if (eventDescriptor.name == "items_gifted") ; //20
        else if (eventDescriptor.name == "jointeam_failed") ; //220 userid reason
        else if (eventDescriptor.name == "map_transition") ; //75
        else if (eventDescriptor.name == "match_end_conditions") ; //189 frags max_rounds win_rounds time
        else if (eventDescriptor.name == "material_default_complete") ; //222
        else if (eventDescriptor.name == "mb_input_lock_cancel") ; //170
        else if (eventDescriptor.name == "mb_input_lock_success") ; //169
        else if (eventDescriptor.name == "molotov_detonate") ; //155 userid x y z
        else if (eventDescriptor.name == "nav_blocked") ; //171 area blocked
        else if (eventDescriptor.name == "nav_generate") ; //172
        else if (eventDescriptor.name == "nextlevel_changed") ; //225 nextlevel
        else if (eventDescriptor.name == "other_death") ; //99 otherid othertype attacker weapon weapon_itemid weapon_fauxitemid weapon_originalowner_xuid headshot penetrated
        else if (eventDescriptor.name == "physgun_pickup") ; //68 entindex
        else if (eventDescriptor.name == "player_activate") ; //10 userid
        else if (eventDescriptor.name == "player_avenged_teammate") ; //185 avenger_id avenged_player_id
        else if (eventDescriptor.name == "player_blind") ; //165 userid
        else if (eventDescriptor.name == "player_changename") ; //30 userid oldname newname
        else if (eventDescriptor.name == "player_chat") ; //25 teamonly userid text
        else if (eventDescriptor.name == "player_class") ; //22 userid class
        else if (eventDescriptor.name == "player_connect") ; //7 name index userid networkid address
        else if (eventDescriptor.name == "player_connect_full") ; //11 userid index
        else if (eventDescriptor.name == "player_death") ; //23 userid attacker assister weapon weapon_itemid weapon_fauxitemid weapon_originalowner_xuid headshot dominated revenge penetrated
        else if (eventDescriptor.name == "player_decal") ; //60 userid
        else if (eventDescriptor.name == "player_disconnect") ; //9 userid reason name networkid
        else if (eventDescriptor.name == "player_falldamage") ; //166 userid damage
        else if (eventDescriptor.name == "player_footstep") ; //163 userid
        else if (eventDescriptor.name == "player_given_c4") ; //210 userid
        else if (eventDescriptor.name == "player_hintmessage") ; //31 hintmessage
        else if (eventDescriptor.name == "player_hurt") ; //24 userid attacker health armor weapon dmg_health dmg_armor hitgroup
        else if (eventDescriptor.name == "player_info") ; //8 name index userid networkid bot
        else if (eventDescriptor.name == "player_jump") ; //164 userid
        else if (eventDescriptor.name == "player_radio") ; //123 userid slot
        else if (eventDescriptor.name == "player_reset_vote") ; //206 userid vote
        else if (eventDescriptor.name == "player_say") ; //12 userid text
        else if (eventDescriptor.name == "player_score") ; //26 userid kills deaths score
        else if (eventDescriptor.name == "player_shoot") ; //28 userid weapon mode
        else if (eventDescriptor.name == "player_spawn") ; //27 userid teamnum
        else if (eventDescriptor.name == "player_spawned") ; //133 userid inrestart
        else if (eventDescriptor.name == "player_stats_updated") ; //63 forceupload
        else if (eventDescriptor.name == "player_team") ; //21 userid team oldteam disconnect autoteam silent isbot
        else if (eventDescriptor.name == "player_use") ; //29 userid entity
        else if (eventDescriptor.name == "ragdoll_dissolved") ; //72 entindex
        else if (eventDescriptor.name == "read_game_titledata") ; //80 controllerId
        else if (eventDescriptor.name == "repost_xbox_achievements") ; //188 splitscreenplayer
        else if (eventDescriptor.name == "reset_game_titledata") ; //82 controllerId
        else if (eventDescriptor.name == "reset_player_controls") ; //219
        else if (eventDescriptor.name == "round_announce_final") ; //38
        else if (eventDescriptor.name == "round_announce_last_round_half") ; //39
        else if (eventDescriptor.name == "round_announce_match_point") ; //37
        else if (eventDescriptor.name == "round_announce_match_start") ; //40
        else if (eventDescriptor.name == "round_announce_warmup") ; //41
        else if (eventDescriptor.name == "round_end") ; //42 winner reason message
        else if (eventDescriptor.name == "round_end_upload_stats") ; //43
        else if (eventDescriptor.name == "round_freeze_end") ; //168
        else if (eventDescriptor.name == "round_officially_ended") ; //44
        else if (eventDescriptor.name == "round_mvp") ; //190 userid reason
        else if (eventDescriptor.name == "round_poststart") ; //149
        else if (eventDescriptor.name == "round_prestart") ; //148
        else if (eventDescriptor.name == "round_start") ; //36 timelimit fraglimit objective
        else if (eventDescriptor.name == "round_start_pre_entity") ; //51
        else if (eventDescriptor.name == "round_time_warning") ; //15
        else if (eventDescriptor.name == "seasoncoin_levelup") ; //226 player category rank
        else if (eventDescriptor.name == "server_addban") ; //5 name userid networkid ip duration by kicked
        else if (eventDescriptor.name == "server_cvar") ; //3 cvarname cvarvalue
        else if (eventDescriptor.name == "server_message") ; //4 text
        else if (eventDescriptor.name == "server_pre_shutdown") ; //1 reason
        else if (eventDescriptor.name == "server_removeban") ; //6 networkid ip by
        else if (eventDescriptor.name == "server_shutdown") ; //2 reason
        else if (eventDescriptor.name == "server_spawn") ; //0 hostname address port game mapname maxplayers os dedicated official password
        else if (eventDescriptor.name == "set_instructor_group_enabled") ; //77 group enabled
        else if (eventDescriptor.name == "sfuievent") ; //208 action data slot
        else if (eventDescriptor.name == "show_freezepanel") ; //182 victim killer hits_taken damage_taken hits_given damage_given
        else if (eventDescriptor.name == "silencer_detach") ; //130 userid
        else if (eventDescriptor.name == "silencer_off") ; //144 userid
        else if (eventDescriptor.name == "silencer_on") ; //145 userid
        else if (eventDescriptor.name == "smokegrenade_detonate") ; //153 userid entityid x y z
        else if (eventDescriptor.name == "smokegrenade_expired") ; //154 userid entityid x y z
        else if (eventDescriptor.name == "spec_mode_updated") ; //175 userid
        else if (eventDescriptor.name == "spec_target_updated") ; //174 userid
        else if (eventDescriptor.name == "start_halftime") ; //228
        else if (eventDescriptor.name == "start_vote") ; //209 userid type vote_parameter
        else if (eventDescriptor.name == "store_pricesheet_updated") ; //95
        else if (eventDescriptor.name == "survival_announce_phase") ; //n58 phase
        else if (eventDescriptor.name == "switch_team") ; //199 numPlayers numSpectators avg_rank numTSlotsFree numCTSlotsFree
        else if (eventDescriptor.name == "tagrenade_detonate") ; //n162 userid entityid x y z
        else if (eventDescriptor.name == "teamplay_broadcast_audio") ; //18 team sound
        else if (eventDescriptor.name == "teamplay_round_start") ; //52 full_reset
        else if (eventDescriptor.name == "team_info") ; //16 teamid teamname
        else if (eventDescriptor.name == "team_score") ; //17 teamid score
        else if (eventDescriptor.name == "teamchange_pending") ; //221 userid toteam
        else if (eventDescriptor.name == "tournament_reward") ; //227 defindex totalrewards accountid
        else if (eventDescriptor.name == "tr_exit_hint_trigger") ; //215
        else if (eventDescriptor.name == "tr_mark_best_time") ; //214 time
        else if (eventDescriptor.name == "tr_mark_complete") ; //213 complete
        else if (eventDescriptor.name == "tr_player_flashbanged") ; //212 userid
        else if (eventDescriptor.name == "tr_show_exit_msgbox") ; //218 userid
        else if (eventDescriptor.name == "tr_show_finish_msgbox") ; //217 userid
        else if (eventDescriptor.name == "trial_time_expired") ; //204 slot
        else if (eventDescriptor.name == "ugc_file_download_start") ; //49 hcontent published_file_id
        else if (eventDescriptor.name == "ugc_map_download_error") ; //47 published_file_id error_code
        else if (eventDescriptor.name == "ugc_map_download_finished") ; //48 hcontent
        else if (eventDescriptor.name == "ugc_map_info_received") ; //45 published_file_id
        else if (eventDescriptor.name == "ugc_map_unsubscribed") ; //46 published_file_id
        else if (eventDescriptor.name == "update_matchmaking_stats") ; //205
        else if (eventDescriptor.name == "user_data_downloaded") ; //71
        else if (eventDescriptor.name == "verify_client_hit") ; //n102 userid pos_x pos_y pos_z timestamp
        else if (eventDescriptor.name == "vip_escaped") ; //121 userid
        else if (eventDescriptor.name == "vip_killed") ; //122 userid attacker
        else if (eventDescriptor.name == "vote_cast") ; //89 vote_option team entityid
        else if (eventDescriptor.name == "vote_changed") ; //86 vote_option1 vote_option2 vote_option3 vote_option4 vote_option5 potentialVotes
        else if (eventDescriptor.name == "vote_ended") ; //84
        else if (eventDescriptor.name == "vote_failed") ; //88 team
        else if (eventDescriptor.name == "vote_options") ; //90 count option1 option2 option3 option4 option5
        else if (eventDescriptor.name == "vote_passed") ; //87 details param1 team
        else if (eventDescriptor.name == "vote_started") ; //85 issue param1 team initiator
        else if (eventDescriptor.name == "weapon_fire") ; //125 userid weapon silenced
        else if (eventDescriptor.name == "weapon_fire_on_empty") ; //126 userid weapon
        else if (eventDescriptor.name == "weapon_outofammo") ; //127 userid
        else if (eventDescriptor.name == "weapon_reload") ; //128 userid
        else if (eventDescriptor.name == "weapon_reload_database") ; //83
        else if (eventDescriptor.name == "weapon_zoom") ; //129 userid
        else if (eventDescriptor.name == "weapon_zoom_rifle") ; //132 userid
        else if (eventDescriptor.name == "write_game_titledata") ; //81 controllerId
        else if (eventDescriptor.name == "write_profile_data") ; //203
        #endregion
    }
    private void ParseCreateStringTable(byte[] data)
    {
        demo.stringTables.Add(StringTable.ParseCreate(data, this));
    }
    private void ParseUpdateStringTable(byte[] data)
    {
        StringTable.ParseUpdate(data, this);
    }
    private void ParseNetTick(byte[] data)
    {
        NetTick.Parse(data);
    }
    #endregion
}

public class DataTables
{
    public SendTable[] sendTables;
    public ServerClass[] serverClasses;
    List<ExcludeEntry> currentExcludes = new List<ExcludeEntry>();
    List<ServerClass> currentBaseClasses = new List<ServerClass>();

    public int ClassBits { get { return serverClasses != null ? Mathf.CeilToInt(Mathf.Log(serverClasses.Length, 2)) : 0; } }

    public static DataTables Parse(byte[] data)
    {
        DataTables dataTables = new DataTables();

        uint currentIndex = 0;
        uint bytesRead = 0;

        List<SendTable> sendTables = new List<SendTable>();
        List<ServerClass> serverClasses = new List<ServerClass>();

        bool endSendTable = false;
        while (!endSendTable)
        {
            SVC_Messages type = (SVC_Messages)(DataParser.ReadProtoInt(data, currentIndex, out bytesRead));
            currentIndex += bytesRead; bytesRead = 0;
            if (type != SVC_Messages.svc_SendTable) throw new Exception("DataTables: Incorrect SVC Message type " + type + " should be SendTable");

            uint tableLength = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            SendTable sendTable = SendTable.Parse(DataParser.ReadBytes(data, currentIndex, tableLength));
            currentIndex += tableLength;

            //Debug.Log("SendTable: " + sendTable.name);
            endSendTable = sendTable.isEnd;
            if (!endSendTable) sendTables.Add(sendTable);
        }

        int serverClassCount = BitConverter.ToUInt16(data, (int)currentIndex);
        currentIndex += 2;

        //Debug.Log("SendTables: " + sendTables.Count + " ServerClasses: " + serverClassCount);
        for(int i = 0; i < serverClassCount; i++)
        {
            ServerClass serverClass = ServerClass.Parse(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;

            //Debug.Log("ServerClass" + i + ": " + serverClass.name + ", " + serverClass.dataTableName + ", " + serverClass.classID + " <= " + serverClassCount);
            if (serverClass.classID >= serverClassCount) throw new Exception("Class index out of bounds");

            serverClass.dataTableID = sendTables.FindIndex(item => item.name == serverClass.dataTableName);

            serverClasses.Add(serverClass);
        }

        dataTables.sendTables = sendTables.ToArray();
        dataTables.serverClasses = serverClasses.ToArray();

        for (int i = 0; i < dataTables.serverClasses.Length; i++)
            dataTables.FlattenDataTable(i);

        return dataTables;
    }

    public void FlattenDataTable(int serverClassIndex)
    {
        SendTable table = sendTables[serverClasses[serverClassIndex].dataTableID];

        currentExcludes.Clear();
        currentBaseClasses = new List<ServerClass>();

        GatherExcludesAndBaseClasses(table, true);

        serverClasses[serverClassIndex].baseClasses = currentBaseClasses.ToArray();

        GatherProperties(table, serverClassIndex, "");

        List<FlattenedPropertyEntry> flattenedProperties = serverClasses[serverClassIndex].flattenedProperties;

        List<int> priorities = new List<int>();
        priorities.Add(64);
        priorities.AddRange(flattenedProperties.Select(item => item.property.priority).Distinct());
        priorities.Sort();

        int start = 0;
        for(int priorityIndex = 0; priorityIndex < priorities.Count; priorityIndex++)
        {
            int priority = priorities[priorityIndex];
            
            while (true)
            {
                int currentProperty = start;

                while(currentProperty < flattenedProperties.Count)
                {
                    SendTableProperty property = flattenedProperties[currentProperty].property;

                    if (property.priority == priority || (priority == 64 && (property.flags & SendTableProperty.SendPropertyFlag.ChangesOften) != 0))
                    {
                        if(start != currentProperty)
                        {
                            FlattenedPropertyEntry temp = flattenedProperties[start];
                            flattenedProperties[start] = flattenedProperties[currentProperty];
                            flattenedProperties[currentProperty] = temp;
                        }

                        start++;
                        break;
                    }
                    currentProperty++;
                }

                if (currentProperty >= flattenedProperties.Count)
                    break;
            }
        }
    }

    public void GatherExcludesAndBaseClasses(SendTable table, bool collectBaseClasses)
    {
        currentExcludes.AddRange(table.properties.Where(item => (item.flags & SendTableProperty.SendPropertyFlag.Exclude) != 0).Select(item => new ExcludeEntry(item.name, item.dataTableName, table.name)));

        foreach(SendTableProperty property in table.properties.Where(item => item.type == SendTableProperty.SendPropertyType.DataTable))
        {
            if(collectBaseClasses && property.name == "baseclass")
            {
                GatherExcludesAndBaseClasses(GetTableByName(property.dataTableName), true);
                currentBaseClasses.Add(FindByDataTableName(property.dataTableName));
            }
            else
            {
                GatherExcludesAndBaseClasses(GetTableByName(property.dataTableName), false);
            }
        }
    }

    public void GatherProperties(SendTable table, int serverClassIndex, string prefix)
    {
        serverClasses[serverClassIndex].flattenedProperties.AddRange(IteratePropertiesInGather(table, serverClassIndex, new List<FlattenedPropertyEntry>(), prefix));
    }
    public List<FlattenedPropertyEntry> IteratePropertiesInGather(SendTable table, int serverClassIndex, List<FlattenedPropertyEntry> flattenedProperties, string prefix)
    {
        for(int i = 0; i < table.properties.Length; i++)
        {
            if ((table.properties[i].flags & SendTableProperty.SendPropertyFlag.InsideArray) != 0 || (table.properties[i].flags & SendTableProperty.SendPropertyFlag.Exclude) != 0 || IsPropertyExcluded(table, table.properties[i]))
                continue;

            if(table.properties[i].type == SendTableProperty.SendPropertyType.DataTable)
            {
                if ((table.properties[i].flags & SendTableProperty.SendPropertyFlag.Collapsible) != 0) IteratePropertiesInGather(sendTables.FirstOrDefault(item => item.name == table.properties[i].dataTableName), serverClassIndex, flattenedProperties, prefix);
                else
                {
                    string nfix = prefix + ((table.properties[i].name.Length > 0) ? table.properties[i].name + "." : "");
                    GatherProperties(sendTables.FirstOrDefault(item => item.name == table.properties[i].dataTableName), serverClassIndex, nfix);
                }
            }
            else
            {
                if(table.properties[i].type == SendTableProperty.SendPropertyType.Array)
                {
                    flattenedProperties.Add(new FlattenedPropertyEntry(prefix + table.properties[i].name, table.properties[i], table.properties[i - 1]));
                }
                else
                {
                    flattenedProperties.Add(new FlattenedPropertyEntry(prefix + table.properties[i].name, table.properties[i], null));
                }
            }
        }

        return flattenedProperties;
    }

    bool IsPropertyExcluded(SendTable table, SendTableProperty property)
    {
        return currentExcludes.Exists(item => table.name == item.dtName && property.name == item.varName);
    }
    SendTable GetTableByName(string propertyName)
    {
        return sendTables.FirstOrDefault(item => item.name == propertyName);
    }

    public ServerClass FindByName(string className)
    {
        return serverClasses.Single(item => item.name == className);
    }
    public ServerClass FindByDataTableName(string dtName)
    {
        return serverClasses.Single(item => item.dataTableName == dtName);
    }
}
public class SendTable
{
    public SendTableProperty[] properties;
    public string name;
    public bool isEnd;
    public bool needsDecoder;

    public static SendTable Parse(byte[] data)
    {
        SendTable table = new SendTable();
        List<SendTableProperty> properties = new List<SendTableProperty>();

        uint currentIndex = 0;
        uint bytesRead = 0;
        while(currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 2)
            {
                if (fieldNum == 2)
                {
                    table.name = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else if (fieldNum == 4)
                {
                    uint propertyLength = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                    properties.Add(SendTableProperty.Parse(DataParser.ReadBytes(data, currentIndex, propertyLength)));
                    currentIndex += propertyLength;
                }
                else throw new Exception("DataTable: Unknown FieldNum and WireType combination");
            }
            else if (wireType == 0)
            {
                int value = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;

                if (fieldNum == 1)
                {
                    table.isEnd = value != 0;
                }
                else if (fieldNum == 3)
                {
                    table.needsDecoder = value != 0;
                }
            }
            else throw new Exception("DataTable: Unexpected WireType");
        }
        table.properties = properties.ToArray();

        #region Debug
        string sendTableDebug = "SendTable: " + table.name + "\n";
        for (int i = 0; i < table.properties.Length; i++)
        {
            sendTableDebug += "\nType: " + table.properties[i].type + " Name: " + table.properties[i].name + " DataTableName: " + table.properties[i].dataTableName;
        }
        //Debug.Log(sendTableDebug);
        #endregion
        return table;
    }
}
public class SendTableProperty
{
    public SendPropertyFlag flags;
    public string name;
    public string dataTableName;
    public float lowValue;
    public float highValue;
    public int numberOfBits;
    public int numberOfElements;
    public int priority;
    public SendPropertyType type;

    public static SendTableProperty Parse(byte[] data)
    {
        SendTableProperty property = new SendTableProperty();

        uint currentIndex = 0;
        uint bytesRead = 0;
        while(currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 2)
            {
                if (fieldNum == 2)
                {
                    property.name = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else if (fieldNum == 5)
                {
                    property.dataTableName = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else throw new Exception("DataTableProperty: Unknown WireType and FieldNum combination");
            }
            else if (wireType == 0)
            {
                int value = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;

                if (fieldNum == 1)
                {
                    property.type = (SendPropertyType)value;
                }
                else if (fieldNum == 3)
                {
                    property.flags = (SendPropertyFlag)value;
                }
                else if (fieldNum == 4)
                {
                    property.priority = value;
                }
                else if (fieldNum == 6)
                {
                    property.numberOfElements = value;
                }
                else if (fieldNum == 9)
                {
                    property.numberOfBits = value;
                }
            }
            else if (wireType == 5)
            {
                float value = BitConverter.ToSingle(data, (int)currentIndex);
                currentIndex += 4;

                if (fieldNum == 7)
                {
                    property.lowValue = value;
                }
                if (fieldNum == 8)
                {
                    property.highValue = value;
                }
            }
            else throw new Exception("DataTableProperty: Unknown WireType");
        }

        return property;
    }

    public enum SendPropertyType
    {
        Int = 0,
        Float = 1,
        Vector = 2,
        VectorXY = 3,
        String = 4,
        Array = 5,
        DataTable = 6,
        Int64 = 7,
    }
    [Flags]
    public enum SendPropertyFlag
    {
        /// <summary>
        /// Unsigned integer data.
        /// </summary>
        Unsigned = (1 << 0),
        /// <summary>
        /// If this is set, the float/vector is treated like a world coordinate. 
        /// Note that the bit count is ignored in this case.
        /// </summary>
        Coord = (1 << 1),
        /// <summary>
        /// For floating point, don't scale into range, just take value as is
        /// </summary>
        NoScale = (1 << 2),
        /// <summary>
        /// For floating point, limit high value to range minus one bit unit
        /// </summary>
        RoundDown = (1 << 3),
        ///<summary>
        /// For floating point, limit low value to range minus one bit unit
        ///</summary>
        RoundUp = (1 << 4),
        ///<summary>
        /// If this is set, the vector is treated like a normal (only valid for vectors)
        ///</summary>
        Normal = (1 << 5),
        ///<summary>
        /// This is an exclude prop (not excludED, but it points at another prop to be excluded).
        ///</summary>
        Exclude = (1 << 6),
        ///<summary>
        /// Use XYZ/Exponent encoding for vectors.
        ///</summary>
        XYZE = (1 << 7),
        ///<summary>
        /// This tells us that the property is inside an array, so it shouldn't be put into the
        /// flattened property list. Its array will point at it when it needs to.
        ///</summary>
        InsideArray = (1 << 8),
        ///<summary>
        /// Set for datatable props using one of the default datatable proxies like
        /// SendProxy_DataTableToDataTable that always send the data to all clients.
        ///</summary>
        ProxyAlwaysYes = (1 << 9),
        ///<summary>
        /// Set automatically if SPROP_VECTORELEM is used.
        ///</summary>
        IsVectorElement = (1 << 10),
        ///<summary>
        /// Set automatically if it's a datatable with an offset of 0 that doesn't change the pointer
        /// (ie: for all automatically-chained base classes).
        /// In this case, it can get rid of this SendPropDataTable altogether and spare the
        /// trouble of walking the hierarchy more than necessary.
        ///</summary>
        Collapsible = (1 << 11),
        ///<summary>
        /// Like SPROP_COORD, but special handling for multiplayer games
        ///</summary>
        CoordMp = (1 << 12),
        /// <summary>
        /// Like SPROP_COORD, but special handling for multiplayer games where the fractional component only gets a 3 bits instead of 5
        /// </summary>
        CoordMpLowPrecision = (1 << 13),
        /// <summary>
        /// SPROP_COORD_MP, but coordinates are rounded to integral boundaries
        /// </summary>
        CoordMpIntegral = (1 << 14),
        /// <summary>
        /// Like SPROP_COORD, but special encoding for cell coordinates that can't be negative, bit count indicate maximum value
        /// </summary>
        CellCoord = (1 << 15),
        /// <summary>
        /// Like SPROP_CELL_COORD, but special handling where the fractional component only gets a 3 bits instead of 5
        /// </summary>
        CellCoordLowPrecision = (1 << 16),
        /// <summary>
        /// SPROP_CELL_COORD, but coordinates are rounded to integral boundaries
        /// </summary>
        CellCoordIntegral = (1 << 17),
        ///<summary>
        /// this is an often changed field, moved to head of sendtable so it gets a small index
        ///</summary>
        ChangesOften = (1 << 18),
        /// <summary>
        /// use var int encoded (google protobuf style), note you want to include SPROP_UNSIGNED if needed, its more efficient
        /// </summary>
        VarInt = (1 << 19)
    }
}
public class ServerClass
{
    public int classID;
    public int dataTableID;
    public string name;
    public string dataTableName;

    public List<FlattenedPropertyEntry> flattenedProperties = new List<FlattenedPropertyEntry>();
    public ServerClass[] baseClasses;

    public static ServerClass Parse(byte[] data, uint startIndex, out uint bytesRead)
    {
        ServerClass serverClass = new ServerClass();

        int stringBytes;

        bytesRead = 0;
        serverClass.classID = BitConverter.ToUInt16(data, (int)startIndex);
        bytesRead += 2;
        serverClass.name = DataParser.ReadDataTableString(data, (int)(startIndex + bytesRead), out stringBytes);
        bytesRead += (uint)stringBytes; stringBytes = 0;
        serverClass.dataTableName = DataParser.ReadDataTableString(data, (int)(startIndex + bytesRead), out stringBytes);
        bytesRead += (uint)stringBytes; stringBytes = 0;

        //Debug.Log("ServerClass\n\nName: " + serverClass.name + " DataTableName: " + serverClass.dataTableName);
        return serverClass;
    }
}
public class ExcludeEntry
{
    public string varName;
    public string dtName;
    public string excludingDT;

    public ExcludeEntry(string varName, string dtName, string excludingDT)
    {
        this.varName = varName;
        this.dtName = dtName;
        this.excludingDT = excludingDT;
    }
}
public class FlattenedPropertyEntry
{
    public string propertyName;
    public SendTableProperty property;
    public SendTableProperty arrayElementProperty;

    public FlattenedPropertyEntry(string propertyName, SendTableProperty property, SendTableProperty arrayElementProperty)
    {
        this.propertyName = propertyName;
        this.property = property;
        this.arrayElementProperty = arrayElementProperty;
    }
}

public class PacketEntities
{
    public int maxEntries;
    public int updatedEntries;
    public bool isDelta;
    public bool updateBaseLine;
    public int baseline;
    public int deltaFrom;

    Tick tick;

    private PacketEntities(Tick tick)
    {
        this.tick = tick;
    }

    public static PacketEntities Parse(byte[] data, Tick tick)
    {
        #region Blazing Blace
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 301 desc 24 IsDelta 0 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 58 len 11055
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 31 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1506 desc 58 len 510
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 23 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1508 desc 58 len 466

        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 26 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1510 desc 58 len 486
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 30 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1512 desc 58 len 579
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 24 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1514 desc 58 len 440
        
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 25 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1516 desc 58 len 449
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 30 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1518 desc 58 len 469
        //desc 8 MaxEntries 533 desc 16 UpdatedEntries 23 desc 24 IsDelta 1 desc 32 UpdateBaseline 0 desc 40 Baseline 0 desc 48 DeltaFrom 1520 desc 58 len 437
        #endregion

        PacketEntities packetEntities = new PacketEntities(tick);

        uint currentIndex = 0;
        uint bytesRead;

        string debugValues = "";
        while(currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead;
            debugValues += "desc " + desc + "\n";
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (fieldNum == 7 && wireType == 2)
            {
                uint dataLength = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead;
                debugValues += "len " + dataLength;

                //if (DemoParser.prints < 9) { Debug.Log(debugValues); DemoParser.prints++; }
                packetEntities.RetrieveEntityData(DataParser.ReadBytes(data, currentIndex, dataLength));
                currentIndex += dataLength;
                break;
            }

            if (wireType != 0)
                throw new Exception("PacketEntities: WireType should be 0");

            int value = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead;

            if (fieldNum == 1) { packetEntities.maxEntries = value; debugValues += "MaxEntries " + value + "\n"; }
            else if (fieldNum == 2) { packetEntities.updatedEntries = value; debugValues += "UpdatedEntries " + value + "\n"; }
            else if (fieldNum == 3) { packetEntities.isDelta = value != 0; debugValues += "IsDelta " + value + "\n"; }
            else if (fieldNum == 4) { packetEntities.updateBaseLine = value != 0; debugValues += "UpdateBaseline " + value + "\n"; }
            else if (fieldNum == 5) { packetEntities.baseline = value; debugValues += "Baseline " + value + "\n"; }
            else if (fieldNum == 6) { packetEntities.deltaFrom = value; debugValues += "DeltaFrom " + value + "\n"; }
        }

        return packetEntities;
    }

    private void RetrieveEntityData(byte[] data)
    {
        uint bitIndex = 0;
        uint bitsRead;
        int entityIndex = -1;

        for(int i = 0; i < updatedEntries; i++)
        {
            entityIndex += 1 + (int)DataParser.ReadUBitInt(data, bitIndex, out bitsRead);
            bitIndex += bitsRead;

            bool currentFlag = DataParser.ReadBit(data, bitIndex);
            bitIndex += 1;
            if(!currentFlag)
            {
                currentFlag = DataParser.ReadBit(data, bitIndex);
                bitIndex += 1;
                if(currentFlag) //Create Entity
                {
                    #region PVS
                    int serverClassID = DataParser.ReadInt(data, bitIndex, (byte)tick.demo.dataTables.ClassBits);
                    bitIndex += (uint)tick.demo.dataTables.ClassBits;
                    //if (DemoParser.prints < 9) Debug.Log("Creating Entity #" + entityIndex + " with ServerClassID: " + serverClassID);

                    ServerClass classOfEntity = tick.demo.dataTables.serverClasses[serverClassID];

                    //DataParser.ReadBits(data, currentBitIndex, 10);
                    bitIndex += 10;

                    Entity createdEntity = new Entity(entityIndex, classOfEntity);

                    object[] fastBaseline;
                    if (tick.demo.preprocessedBaselines.TryGetValue(serverClassID, out fastBaseline))
                    {
                        Entity.PropertyEntry.Emit(createdEntity, fastBaseline);
                    }
                    else
                    {
                        List<object> preprocessedBaseline = new List<object>();
                        if (tick.demo.instanceBaselines.ContainsKey(serverClassID))
                        {
                            using (PropertyCollector collector = new PropertyCollector(createdEntity, preprocessedBaseline))
                            {
                                //if (DemoParser.prints < 9) Debug.Log("Applying Update from PreprocessedBaseline");
                                byte[] instanceBaseline = tick.demo.instanceBaselines[serverClassID];
                                createdEntity.Update(instanceBaseline, 0, out bitsRead);
                            }
                        }
                        tick.demo.preprocessedBaselines.Add(serverClassID, preprocessedBaseline.ToArray());
                    }
                    #region Blazing Blace
                    //ServerClassID: 236  Not Emitting  Applying Update
                    //ServerClassID: 34  Not Emitting  Applying Update
                    //ServerClassID: 34  Emitting
                    //ServerClassID: 34  Emitting
                    //ServerClassID: 34  Emitting
                    //ServerClassID: 37  Not Emitting  Applying Update
                    //ServerClassID: 37  Emitting
                    #endregion
                    #endregion

                    tick.demo.entities[entityIndex] = createdEntity;

                    #region Debug Unique Entities
                    DemoParser.uniqueEntities[createdEntity.serverClass] = createdEntity;
                    #endregion

                    createdEntity.Update(data, bitIndex, out bitsRead);
                    bitIndex += bitsRead;
                }
                else //Update Entity
                {
                    Entity entity = tick.demo.entities[entityIndex];
                    entity.Update(data, bitIndex, out bitsRead);
                    bitIndex += bitsRead;

                    #region Debug Unique Entities
                    DemoParser.uniqueEntities[entity.serverClass] = entity;
                    #endregion
                }
            }
            else //Destroy Entity
            {
                tick.demo.entities[entityIndex].Destroy();
                tick.demo.entities.Remove(entityIndex);

                bitIndex += 1;
            }
        }
    }

    private class PropertyCollector : IDisposable
    {
        private readonly Entity underlying;
        private readonly IList<object> capture;

        public PropertyCollector(Entity underlying, IList<object> capture)
        {
            this.underlying = underlying;
            this.capture = capture;

            foreach(Entity.PropertyEntry property in this.underlying.properties)
            {
                if (property.entry.property.type == SendTableProperty.SendPropertyType.Array)
                    property.ArrayReceived += HandleArrayReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Float)
                    property.FloatReceived += HandleFloatReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Int)
                    property.IntReceived += HandleIntReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.String)
                    property.StringReceived += HandleStringReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Vector || property.entry.property.type == SendTableProperty.SendPropertyType.VectorXY)
                    property.VectorReceived += HandleVectorReceived;
                else throw new Exception("Cannot handle unknown type");
            }
        }

        private void HandleVectorReceived(object sender, Entity.PropertyEntry.PropertyUpdateEventArgs<Vector3> eventArgs) { capture.Add(eventArgs.Record()); }
        private void HandleStringReceived(object sender, Entity.PropertyEntry.PropertyUpdateEventArgs<string> eventArgs) { capture.Add(eventArgs.Record()); }
        private void HandleIntReceived(object sender, Entity.PropertyEntry.PropertyUpdateEventArgs<int> eventArgs) { capture.Add(eventArgs.Record()); }
        private void HandleFloatReceived(object sender, Entity.PropertyEntry.PropertyUpdateEventArgs<float> eventArgs) { capture.Add(eventArgs.Record()); }
        private void HandleArrayReceived(object sender, Entity.PropertyEntry.PropertyUpdateEventArgs<object[]> eventArgs) { capture.Add(eventArgs.Record()); }

        public void Dispose()
        {
            foreach(Entity.PropertyEntry property in underlying.properties)
            {
                if (property.entry.property.type == SendTableProperty.SendPropertyType.Array)
                    property.ArrayReceived -= HandleArrayReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Float)
                    property.FloatReceived -= HandleFloatReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Int)
                    property.IntReceived -= HandleIntReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.String)
                    property.StringReceived -= HandleStringReceived;
                else if (property.entry.property.type == SendTableProperty.SendPropertyType.Vector || property.entry.property.type == SendTableProperty.SendPropertyType.VectorXY)
                    property.VectorReceived -= HandleVectorReceived;
                else throw new Exception("Cannot handle unknown type");
            }
        }
    }
}
public class PropertyDecoder
{
    public static object DecodeProperty(FlattenedPropertyEntry property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        SendTableProperty sendTableProperty = property.property;
        if (sendTableProperty.type == SendTableProperty.SendPropertyType.Int) return DecodeInt(sendTableProperty, data, bitIndex, out bitsRead);
        else if (sendTableProperty.type == SendTableProperty.SendPropertyType.Float) return DecodeFloat(sendTableProperty, data, bitIndex, out bitsRead);
        else if (sendTableProperty.type == SendTableProperty.SendPropertyType.Vector) return DecodeVector(sendTableProperty, data, bitIndex, out bitsRead);
        else if (sendTableProperty.type == SendTableProperty.SendPropertyType.Array) return DecodeArray(property, data, bitIndex, out bitsRead);
        else if (sendTableProperty.type == SendTableProperty.SendPropertyType.String) return DecodeString(sendTableProperty, data, bitIndex, out bitsRead);
        else if (sendTableProperty.type == SendTableProperty.SendPropertyType.VectorXY) return DecodeVectorXY(sendTableProperty, data, bitIndex, out bitsRead);
        else throw new Exception("Cannot decode: Unknown type");
    }

    public static int DecodeInt(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        if ((property.flags & SendTableProperty.SendPropertyFlag.VarInt) != 0)
        {
            //Debug.Log("VarInteger");
            //if ((property.flags & SendTableProperty.SendPropertyFlag.Unsigned) != 0)
                return (int)DataParser.ReadVarInt32(data, bitIndex, out bitsRead);
        }
        else
        {
            //Debug.Log("Integer");
            bitsRead = (byte)property.numberOfBits;
            //if ((property.flags & SendTableProperty.SendPropertyFlag.Unsigned) != 0)
                return DataParser.ReadInt(data, bitIndex, (byte)property.numberOfBits);
        }
    }
    public static float DecodeFloat(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        //Debug.Log("Float");
        float resultValue = 0f;
        ulong dwInterp;

        if (DecodeSpecialFloat(property, data, bitIndex, out bitsRead, out resultValue))
            return resultValue;

        dwInterp = DataParser.ReadUInt(data, bitIndex + bitsRead, (byte)property.numberOfBits);
        bitsRead += (byte)property.numberOfBits;

        resultValue = (float)dwInterp / ((1 << property.numberOfBits) - 1);
        resultValue = property.lowValue + (property.highValue - property.lowValue) * resultValue;

        return resultValue;
    }
    public static Vector3 DecodeVector(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        //Debug.Log("Vector");
        uint tempBitsRead = 0;
        float x = DecodeFloat(property, data, bitIndex, out tempBitsRead);
        bitsRead = tempBitsRead;
        float y = DecodeFloat(property, data, bitIndex + bitsRead, out tempBitsRead);
        bitsRead += tempBitsRead;
        float z = 0;

        if ((property.flags & SendTableProperty.SendPropertyFlag.Normal) == 0)
        {
            z = DecodeFloat(property, data, bitIndex + bitsRead, out tempBitsRead);
            bitsRead += tempBitsRead;
        }
        else
        {
            bool isNegative = DataParser.ReadBit(data, bitIndex + bitsRead);
            bitsRead += 1;

            float absolute = x * x + y * y;
            if (absolute < 1)
                z = Mathf.Sqrt(1 - absolute);

            if (isNegative)
                z *= -1;
        }

        return new Vector3(x, y, z);
    }
    public static object[] DecodeArray(FlattenedPropertyEntry flattenedProperty, byte[] data, uint bitIndex, out uint bitsRead)
    {
        //Debug.Log("Array");
        int maxElements = flattenedProperty.property.numberOfElements;

        byte numBits = 1;

        while ((maxElements >>= 1) != 0)
            numBits++;

        int numElements = DataParser.ReadInt(data, bitIndex, numBits);
        bitsRead = numBits;

        object[] result = new object[numElements];

        FlattenedPropertyEntry temp = new FlattenedPropertyEntry("", flattenedProperty.arrayElementProperty, null);
        for (int i = 0; i < numElements; i++)
        {
            uint tempBitsRead;
            result[i] = DecodeProperty(temp, data, bitIndex + bitsRead, out tempBitsRead);
            bitsRead += tempBitsRead;
        }

        return result;
    }
    public static string DecodeString(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        //Debug.Log("String");
        int stringByteSize = DataParser.ReadInt(data, bitIndex, 9);
        bitsRead = 9;
        byte[] stringBytes = DataParser.ReadBits(data, bitIndex + bitsRead, (uint)(stringByteSize * 8));
        bitsRead += (uint)(stringByteSize * 8);
        return System.Text.Encoding.Default.GetString(stringBytes);
    }
    public static Vector3 DecodeVectorXY(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead)
    {
        //Debug.Log("VectorXY");
        uint tempBitsRead;
        float x = DecodeFloat(property, data, bitIndex, out tempBitsRead);
        bitsRead = tempBitsRead;
        float y = DecodeFloat(property, data, bitIndex + bitsRead, out tempBitsRead);
        bitsRead += tempBitsRead;

        return new Vector3(x, y);
    }

    #region Special Float Stuff
    private static bool DecodeSpecialFloat(SendTableProperty property, byte[] data, uint bitIndex, out uint bitsRead, out float result)
    {
        if ((property.flags & SendTableProperty.SendPropertyFlag.Coord) != 0)
        {
            result = ReadBitCoord(data, bitIndex, out bitsRead);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CoordMp) != 0)
        {
            result = ReadBitCoordMP(data, bitIndex, out bitsRead, false, false);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CoordMpLowPrecision) != 0)
        {
            result = ReadBitCoordMP(data, bitIndex, out bitsRead, false, true);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CoordMpIntegral) != 0)
        {
            result = ReadBitCoordMP(data, bitIndex, out bitsRead, true, false);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.NoScale) != 0)
        {
            result = DataParser.ReadFloat(data, bitIndex);
            bitsRead = 32;
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.Normal) != 0)
        {
            result = ReadBitNormal(data, bitIndex, out bitsRead);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CellCoord) != 0)
        {
            result = ReadBitCellCoord(data, bitIndex, out bitsRead, (byte)property.numberOfBits, false, false);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CellCoordLowPrecision) != 0)
        {
            result = ReadBitCellCoord(data, bitIndex, out bitsRead, (byte)property.numberOfBits, true, false);
            return true;
        }
        else if ((property.flags & SendTableProperty.SendPropertyFlag.CellCoordIntegral) != 0)
        {
            result = ReadBitCellCoord(data, bitIndex, out bitsRead, (byte)property.numberOfBits, false, true);
            return true;
        }

        bitsRead = 0;
        result = 0;
        return false;
    }

    static readonly byte COORD_FRACTIONAL_BITS = 5;
    static readonly byte COORD_DENOMINATOR = (byte)(1 << (COORD_FRACTIONAL_BITS));
    static readonly float COORD_RESOLUTION = (1.0f / (COORD_DENOMINATOR));

    static readonly byte COORD_FRACTIONAL_BITS_MP_LOWPRECISION = 3;
    static readonly float COORD_DENOMINATOR_LOWPRECISION = (1 << (COORD_FRACTIONAL_BITS_MP_LOWPRECISION));
    static readonly float COORD_RESOLUTION_LOWPRECISION = (1.0f / (COORD_DENOMINATOR_LOWPRECISION));

    private static float ReadBitCoord(byte[] data, uint bitIndex, out uint bitsRead)
    {
        int intVal, fractVal;
        float value = 0;

        bool isNegative = false;

        intVal = DataParser.ReadInt(data, bitIndex, 1);
        bitsRead = 1;
        fractVal = DataParser.ReadInt(data, bitIndex + bitsRead, 1);
        bitsRead += 1;

        if ((intVal | fractVal) != 0)
        {
            isNegative = DataParser.ReadBit(data, bitIndex + bitsRead);
            bitsRead += 1;

            if (intVal == 1)
            {
                intVal = DataParser.ReadInt(data, bitIndex + bitsRead, 14) + 1;
                bitsRead += 14;
            }

            if (fractVal == 1)
            {
                fractVal = DataParser.ReadInt(data, bitIndex + bitsRead, COORD_FRACTIONAL_BITS);
                bitsRead += COORD_FRACTIONAL_BITS;
            }

            value = intVal + (fractVal * COORD_RESOLUTION);
        }

        if (isNegative)
            value *= -1;

        return value;
    }
    private static float ReadBitCoordMP(byte[] data, uint bitIndex, out uint bitsRead, bool isIntegral, bool isLowPrecision)
    {
        int intVal = 0, fractVal = 0;
        float value = 0;
        bool isNegative = false;

        bool inBounds = DataParser.ReadBit(data, bitIndex);
        bitsRead = 1;

        if(isIntegral)
        {
            intVal = DataParser.ReadBit(data, bitIndex + bitsRead) ? 1 : 0;
            bitsRead += 1;

            if(intVal == 1)
            {
                isNegative = DataParser.ReadBit(data, bitIndex + bitsRead);
                bitsRead += 1;

                if(inBounds)
                {
                    value = DataParser.ReadUInt(data, bitIndex + bitsRead, 11) + 1;
                    bitsRead += 11;
                }
                else
                {
                    value = DataParser.ReadUInt(data, bitIndex + bitsRead, 14) + 1;
                    bitsRead += 14;
                }
            }
        }
        else
        {
            intVal = DataParser.ReadBit(data, bitIndex + bitsRead) ? 1 : 0;
            bitsRead += 1;

            isNegative = DataParser.ReadBit(data, bitIndex + bitsRead);
            bitsRead += 1;

            if(intVal == 1)
            {
                if(inBounds)
                {
                    value = DataParser.ReadUInt(data, bitIndex + bitsRead, 11) + 1;
                    bitsRead += 11;
                }
                else
                {
                    value = DataParser.ReadUInt(data, bitIndex + bitsRead, 14) + 1;
                    bitsRead += 14;
                }
            }

            byte fractBitSize = (byte)(isLowPrecision ? 3 : 5);
            fractVal = DataParser.ReadInt(data, bitIndex + bitsRead, fractBitSize);
            bitsRead += fractBitSize;

            value = intVal + (fractVal * (isLowPrecision ? COORD_RESOLUTION_LOWPRECISION : COORD_RESOLUTION));
        }

        if (isNegative)
            value *= -1;

        return value;
    }
    private static float ReadBitCellCoord(byte[] data, uint bitIndex, out uint bitsRead, byte bits, bool lowPrecision, bool integral)
    {
        int intVal = 0, fractVal = 0;
        float value = 0;

        if(integral)
        {
            value = DataParser.ReadUInt(data, bitIndex, bits);
            bitsRead = bits;
        }
        else
        {
            intVal = DataParser.ReadInt(data, bitIndex, bits);
            bitsRead = bits;
            byte fractBitSize = lowPrecision ? COORD_FRACTIONAL_BITS_MP_LOWPRECISION : COORD_FRACTIONAL_BITS;
            fractVal = DataParser.ReadInt(data, bitIndex + bitsRead, fractBitSize);
            bitsRead += fractBitSize;

            value = intVal + (fractVal * (lowPrecision ? COORD_RESOLUTION_LOWPRECISION : COORD_RESOLUTION));
        }

        return value;
    }

    static readonly byte NORMAL_FRACTIONAL_BITS = 11;
    static readonly uint NORMAL_DENOMINATOR = (uint)((1 << (NORMAL_FRACTIONAL_BITS)) - 1);
    static readonly float NORMAL_RESOLUTION = (1.0f / (NORMAL_DENOMINATOR));

    private static float ReadBitNormal(byte[] data, uint bitIndex, out uint bitsRead)
    {
        bool isNegative = DataParser.ReadBit(data, bitIndex);
        bitsRead = 1;

        uint fractVal = DataParser.ReadUInt(data, bitIndex + bitsRead, NORMAL_FRACTIONAL_BITS);
        bitsRead += NORMAL_FRACTIONAL_BITS;

        float value = fractVal * NORMAL_RESOLUTION;

        if (isNegative)
            value *= -1;

        return value;
    }
    #endregion
}
public class Entity
{
    public int id;
    public ServerClass serverClass;
    public PropertyEntry[] properties;

    public Entity(int id, ServerClass serverClass)
    {
        this.id = id;
        this.serverClass = serverClass;

        properties = new PropertyEntry[serverClass.flattenedProperties.Count];
        for (int i = 0; i < properties.Length; i++)
            properties[i] = new PropertyEntry(serverClass.flattenedProperties[i], (uint)i);
    }

    public PropertyEntry FindProperty(string name)
    {
        return properties.Single(item => item.entry.propertyName == name);
    }

    public void Update(byte[] data, uint bitIndex, out uint bitsRead)
    {
        bool newWay = DataParser.ReadBit(data, bitIndex);
        bitsRead = 1;
        int index = -1;
        List<PropertyEntry> entries = new List<PropertyEntry>();

        //uint entriesAdded = 0;

        uint tempBitsRead;
        index = ReadFieldIndex(data, bitIndex + bitsRead, out tempBitsRead, index, newWay);
        bitsRead += tempBitsRead;
        while (index != -1)
        {
            //entriesAdded++;
            #region Blazing Blace
            //if (DemoParser.prints < 9) Debug.Log("Index: " + index); //0 1 2...57  0 1 2...1019  0 1 2 3 7 8 17 18 19 301 541 597 611 615 616
            #endregion
            entries.Add(properties[index]);

            index = ReadFieldIndex(data, bitIndex + bitsRead, out tempBitsRead, index, newWay);
            bitsRead += tempBitsRead;
        }

        #region Blazing Blace
        //Creating Entity #0 | Applying Update from PreprocessedBaseline | Added 57 entries. There are now 57 entries in the list | Applying Update from Data | Added 0 entries. There are no 0 entries in the list
        //Creating Entity #1 | Applying Update from PreprocessedBaseline | Added 1020 entries. There are now 1020 entries in the list | Applying Update from Data | Added 15 entries. There are now 15 entries in the list
        //Applying from Data | Int = 6 | Int = 1507 | VectorXY = (1376.0, 2832.0, 0.0) | Float = 240 | VectorXY = (1376.0, 2832.0, 0.0) | Float = 240 | Float = 18.76563 | Float = 13.35938 | Float = 238.3594 | Int = 18 | Int = 32 | Int = 3 | String = BackofA | Int = 542795 | Int = 1572938
        //Creating Entity #2 | Applying Update from Data | Added 65 entries. There are now 65 entries in the list
        //Value = 8 | Value = 1509 | Value = (-467.7, -657.3, 0.0) | Value = 127.4526 | Value = 46.04497 | Value = 263 | Value = 10 | Value = 22.76563 | Value = 4.921875 | Value = 88.94531 | Value = 23.04688 | Value = 14 | Value = 22.04688 | Value = 11412277 | Value = 463340
        //Creating Entity #3 | Applying Update from Data | Added 12 entries. There are now 12 entries in the list
        //Creating Entity #4 | Applying Update from Data | Added 20 entries. There are now 20 entries in the list
        //Creating Entity #12 | Applying Update from Data | Added 75 entries. There are now 75 entries in the list
        //Creating Entity #13 | Applying Update from Data | Added 71 entries. There are now 71 entries in the list
        //if (DemoParser.prints < 9) Debug.Log("Added " + entriesAdded + " entries. There are now " + entries.Count + " entries in the list");
        #endregion

        foreach (PropertyEntry property in entries)
        {
            property.Decode(data, bitIndex + bitsRead, out tempBitsRead, this);
            bitsRead += tempBitsRead;
        }
    }

    private int ReadFieldIndex(byte[] data, uint bitIndex, out uint bitsRead, int lastIndex, bool bNewWay)
    {
        bitsRead = 0;
        bool tempBool;
        if(bNewWay)
        {
            tempBool = DataParser.ReadBit(data, bitIndex);
            bitsRead += 1;
            if (tempBool)
            {
                return lastIndex + 1;
            }
        }

        int ret = 0;
        tempBool = DataParser.ReadBit(data, bitIndex + bitsRead);
        bitsRead += 1;
        if (bNewWay && tempBool)
        {
            ret = DataParser.ReadInt(data, bitIndex + bitsRead, 3);
            bitsRead += 3;
        }
        else
        {
            ret = DataParser.ReadInt(data, bitIndex + bitsRead, 7);
            bitsRead += 7;

            if((ret & (32 | 64)) == 32)
            {
                ret = (ret & ~96) | (DataParser.ReadInt(data, bitIndex + bitsRead, 2) << 5);
                bitsRead += 2;
            }
            else if((ret & (32 | 64)) == 64)
            {
                ret = (ret & ~96) | (DataParser.ReadInt(data, bitIndex + bitsRead, 4) << 5);
                bitsRead += 4;
            }
            else if ((ret & (32 | 64)) == 96)
            {
                ret = (ret & ~96) | (DataParser.ReadInt(data, bitIndex + bitsRead, 7) << 5);
                bitsRead += 7;
            }
        }

        if (ret == 0xfff)
            return -1;

        return lastIndex + 1 + ret;
    }

    public void Destroy()
    {
        foreach (PropertyEntry property in properties)
            property.Destroy();
    }

    public override string ToString()
    {
        return id + ": " + serverClass;
    }

    public class PropertyEntry
    {
        public readonly uint index;
        public FlattenedPropertyEntry entry { get; private set; }
        public object value;

        public PropertyEntry(FlattenedPropertyEntry property, uint index)
        {
            entry = new FlattenedPropertyEntry(property.propertyName, property.property, property.arrayElementProperty);
            this.index = index;
        }

        public void Decode(byte[] data, uint bitIndex, out uint totalBitsRead, Entity entity)
        {
            totalBitsRead = 0;
            if (entry.property.type == SendTableProperty.SendPropertyType.Int)
            {
                value = PropertyDecoder.DecodeInt(entry.property, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("Int: " + value);
                if (IntReceived != null)
                    IntReceived(this, new PropertyUpdateEventArgs<int>((int)value, entity, this));
            }
            else if (entry.property.type == SendTableProperty.SendPropertyType.Float)
            {
                value = PropertyDecoder.DecodeFloat(entry.property, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("Float: " + value);
                if (FloatReceived != null)
                    FloatReceived(this, new PropertyUpdateEventArgs<float>((float)value, entity, this));
            }
            else if (entry.property.type == SendTableProperty.SendPropertyType.Vector)
            {
                value = PropertyDecoder.DecodeVector(entry.property, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("Vector: " + value);
                if (VectorReceived != null)
                    VectorReceived(this, new PropertyUpdateEventArgs<Vector3>((Vector3)value, entity, this));
            }
            else if (entry.property.type == SendTableProperty.SendPropertyType.Array)
            {
                value = PropertyDecoder.DecodeArray(entry, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("Array: " + value);
                if (ArrayReceived != null)
                    ArrayReceived(this, new PropertyUpdateEventArgs<object[]>((object[])value, entity, this));
            }
            else if (entry.property.type == SendTableProperty.SendPropertyType.String)
            {
                value = PropertyDecoder.DecodeString(entry.property, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("String: " + value);
                if (StringReceived != null)
                    StringReceived(this, new PropertyUpdateEventArgs<string>((string)value, entity, this));
            }
            else if (entry.property.type == SendTableProperty.SendPropertyType.VectorXY)
            {
                value = PropertyDecoder.DecodeVectorXY(entry.property, data, bitIndex, out totalBitsRead);
                //if (DemoParser.prints < 9) Debug.Log("VectorXY: " + value);
                if (VectorReceived != null)
                    VectorReceived(this, new PropertyUpdateEventArgs<Vector3>((Vector3)value, entity, this));
            }
            else throw new Exception("Could not read property.");
        }

        public static void Emit(Entity entity, object[] captured)
        {
            foreach(object arg in captured)
            {
                RecordedPropertyUpdate<int> intReceived = arg as RecordedPropertyUpdate<int>;
                RecordedPropertyUpdate<float> floatReceived = arg as RecordedPropertyUpdate<float>;
                RecordedPropertyUpdate<Vector3> vectorReceived = arg as RecordedPropertyUpdate<Vector3>;
                RecordedPropertyUpdate<string> stringReceived = arg as RecordedPropertyUpdate<string>;
                RecordedPropertyUpdate<object[]> arrayReceived = arg as RecordedPropertyUpdate<object[]>;

                if (intReceived != null)
                {
                    EventHandler<PropertyUpdateEventArgs<int>> eventHandler = entity.properties[intReceived.propertyIndex].IntReceived;
                    if (eventHandler != null)
                        eventHandler(null, new PropertyUpdateEventArgs<int>(intReceived.value, entity, entity.properties[intReceived.propertyIndex]));
                }
                else if (floatReceived != null)
                {
                    EventHandler<PropertyUpdateEventArgs<float>> eventHandler = entity.properties[floatReceived.propertyIndex].FloatReceived;
                    if (eventHandler != null)
                        eventHandler(null, new PropertyUpdateEventArgs<float>(floatReceived.value, entity, entity.properties[floatReceived.propertyIndex]));
                }
                else if (vectorReceived != null)
                {
                    EventHandler<PropertyUpdateEventArgs<Vector3>> eventHandler = entity.properties[vectorReceived.propertyIndex].VectorReceived;
                    if (eventHandler != null)
                        eventHandler(null, new PropertyUpdateEventArgs<Vector3>(vectorReceived.value, entity, entity.properties[vectorReceived.propertyIndex]));
                }
                else if (stringReceived != null)
                {
                    EventHandler<PropertyUpdateEventArgs<string>> eventHandler = entity.properties[stringReceived.propertyIndex].StringReceived;
                    if (eventHandler != null)
                        eventHandler(null, new PropertyUpdateEventArgs<string>(stringReceived.value, entity, entity.properties[stringReceived.propertyIndex]));
                }
                else if (arrayReceived != null)
                {
                    EventHandler<PropertyUpdateEventArgs<object[]>> eventHandler = entity.properties[arrayReceived.propertyIndex].ArrayReceived;
                    if (eventHandler != null)
                        eventHandler(null, new PropertyUpdateEventArgs<object[]>(arrayReceived.value, entity, entity.properties[arrayReceived.propertyIndex]));
                }
                else throw new Exception("Cannot emit unknown type");
            }
        }

        public void Destroy()
        {
            IntReceived = null;
            FloatReceived = null;
            ArrayReceived = null;
            StringReceived = null;
            VectorReceived = null;
        }

        #region Events
        public event EventHandler<PropertyUpdateEventArgs<int>> IntReceived;
        public event EventHandler<PropertyUpdateEventArgs<float>> FloatReceived;
        public event EventHandler<PropertyUpdateEventArgs<Vector3>> VectorReceived;
        public event EventHandler<PropertyUpdateEventArgs<string>> StringReceived;
        public event EventHandler<PropertyUpdateEventArgs<object[]>> ArrayReceived;

        public class PropertyUpdateEventArgs<T> : EventArgs
        {
            public T value { get; private set; }
            public Entity entity { get; private set; }
            public PropertyEntry property { get; private set; }
            
            public PropertyUpdateEventArgs(T value, Entity entity, PropertyEntry property)
            {
                this.value = value;
                this.entity = entity;
                this.property = property;
            }

            public RecordedPropertyUpdate<T> Record()
            {
                return new RecordedPropertyUpdate<T>(property.index, value);
            }
        }

        public class RecordedPropertyUpdate<T>
        {
            public uint propertyIndex;
            public T value;

            public RecordedPropertyUpdate(uint propertyIndex, T value)
            {
                this.propertyIndex = propertyIndex;
                this.value = value;
            }
        }
        #endregion
    }
}

public class EventDescriptor
{
    public int eventID;
    public string name;
    public EventKey[] keys;

    public static EventDescriptor Parse(byte[] data)
    {
        EventDescriptor descriptor = new EventDescriptor();
        List<EventKey> keys = new List<EventKey>();

        uint currentIndex = 0;
        uint bytesRead = 0;

        while (currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 0 && fieldNum == 1)
            {
                descriptor.eventID = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else if (wireType == 2 && fieldNum == 2)
            {
                descriptor.name = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else if (wireType == 2 && fieldNum == 3)
            {
                uint keyLength = (uint)DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
                keys.Add(EventKey.Parse(DataParser.ReadBytes(data, currentIndex, keyLength)));
                currentIndex += keyLength;
            }
            else throw new Exception("GameEventListDescriptor: Unknown WireType and FieldNum combination");
        }
        descriptor.keys = keys.ToArray();

        return descriptor;
    }
}
public class EventKey
{
    public int type;
    public string name;

    public static EventKey Parse(byte[] data)
    {
        EventKey key = new EventKey();

        uint currentIndex = 0;
        uint bytesRead = 0;

        while(currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 0 && fieldNum == 1)
            {
                key.type = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else if (wireType == 2 && fieldNum == 2)
            {
                key.name = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else throw new Exception("GameEventListDescriptorKey: Unknown WireType and FieldNum combination");
        }
        
        return key;
    }

    //public override bool Equals(object obj)
    //{
    //    return (obj is EventKey) ? (((EventKey)obj).type == type && ((EventKey)obj).name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) : false;
    //}
}
public class GameEvent
{
    public string eventName;
    public int eventID;
    public object[] keys;

    public static GameEvent Parse(byte[] data)
    {
        GameEvent gameEvent = new GameEvent();
        List<object> keys = new List<object>();

        uint currentIndex = 0;
        uint bytesRead = 0;

        while (currentIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
            currentIndex += bytesRead; bytesRead = 0;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;
            if ((wireType == 2) && (fieldNum == 1))
            {
                gameEvent.eventName = DataParser.ReadProtoString(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else if ((wireType == 0) && (fieldNum == 2))
            {
                gameEvent.eventID = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
            }
            else if ((wireType == 2) && (fieldNum == 3))
            {
                int keySize = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
                uint keyStartIndex = currentIndex;

                desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
                wireType = (byte)(desc & 7);
                fieldNum = desc >> 3;

                if ((wireType != 0) || (fieldNum != 1)) throw new Exception("GameEvent: WireType does not equal zero or FieldNum does not equal one");

                int typeMember = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;

                desc = DataParser.ReadProtoInt(data, currentIndex, out bytesRead);
                currentIndex += bytesRead; bytesRead = 0;
                wireType = (byte)(desc & 7);
                fieldNum = desc >> 3;

                if (fieldNum != (typeMember + 1)) throw new Exception("GameEvent: FieldNum does not equal (TypeMember + 1)");

                if (typeMember == 1) //string
                {
                    if (wireType != 2) throw new Exception("GameEvent: TypeMember 1, WireType should be 2");
                    keys.Add(DataParser.ReadProtoString(data, currentIndex, out bytesRead));
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else if (typeMember == 2) //float
                {
                    if (wireType != 5) throw new Exception("GameEvent: TypeMember 2, WireType should be 5");
                    keys.Add(BitConverter.ToSingle(data, (int)currentIndex));
                    currentIndex += 4;
                }
                else if (typeMember == 3 || typeMember == 4 || typeMember == 5) //long/short/byte
                {
                    if (wireType != 0) throw new Exception("GameEvent: TypeMember 3 4 or 5, WireType should be 0");
                    keys.Add(DataParser.ReadProtoInt(data, currentIndex, out bytesRead));
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else if (typeMember == 6) //bool
                {
                    if (wireType != 0) throw new Exception("GameEvent: TypeMember 6, WireType should be 0");
                    keys.Add(DataParser.ReadProtoInt(data, currentIndex, out bytesRead) != 0);
                    currentIndex += bytesRead; bytesRead = 0;
                }
                else throw new Exception("GameEvent: Unknown TypeMember");

                if (currentIndex - keyStartIndex < keySize) throw new Exception("GameEvent: Key Data larger than expected");
            }
        }
        gameEvent.keys = keys.ToArray();

        //Debug.Log("GameEvent ID: " + gameEvent.eventID + " Name: " + gameEvent.eventName + " Keys: " + gameEvent.keys.Length);
        return gameEvent;
    }

    public static Dictionary<string, object> MapData(EventDescriptor eventDescriptor, GameEvent currentEvent)
    {
        Dictionary<string, object> data = new Dictionary<string, object>();

        for (int i = 0; i < eventDescriptor.keys.Length; i++)
            data.Add(eventDescriptor.keys[i].name, currentEvent.keys[i]);

        return data;
    }
}

public class PlayerInfo
{
    public long version;
    public long xuid;
    public string name;
    public int userID;
    public string guid;
    public int friendsID;
    public string friendsName;
    public bool isFakePlayer;
    public bool isHLTV;

    public int customFiles0;
    public int customFiles1;
    public int customFiles2;
    public int customFiles3;

    byte filesDownloaded;

    public static PlayerInfo Parse(byte[] data)
    {
        PlayerInfo playerInfo = new PlayerInfo();

        uint byteIndex = 0;

        DataParser.bigEndian = true;
        playerInfo.version = DataParser.ReadLong(data, byteIndex * 8); byteIndex += 8;
        playerInfo.xuid = DataParser.ReadLong(data, byteIndex * 8); byteIndex += 8;
        DataParser.bigEndian = false;
        playerInfo.name = DataParser.ReadCString(data, byteIndex, 128); byteIndex += 128;
        DataParser.bigEndian = true;
        playerInfo.userID = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;
        DataParser.bigEndian = false;
        playerInfo.guid = DataParser.ReadCString(data, byteIndex, 33); byteIndex += 33;
        DataParser.bigEndian = true;
        playerInfo.friendsID = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;
        DataParser.bigEndian = false;
        playerInfo.friendsName = DataParser.ReadCString(data, byteIndex, 128); byteIndex += 128;

        playerInfo.isFakePlayer = DataParser.ReadBool(data, byteIndex * 8); byteIndex += 1;
        playerInfo.isHLTV = DataParser.ReadBool(data, byteIndex * 8); byteIndex += 1;

        playerInfo.customFiles0 = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;
        playerInfo.customFiles1 = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;
        playerInfo.customFiles2 = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;
        playerInfo.customFiles3 = DataParser.ReadInt(data, byteIndex * 8); byteIndex += 4;

        playerInfo.filesDownloaded = DataParser.ReadByte(data, byteIndex * 8); byteIndex += 1;

        return playerInfo;
    }
}
public class StringTable
{
    public Tick tick;
    public string name;
    public int maxEntries;
    public int numEntries;
    public bool userDataFixedSize;
    public int userDataSize;
    public int userDataSizeBits;
    public int flags;

    public static StringTable ParseCreate(byte[] data, Tick tick)
    {
        StringTable stringTable = new StringTable();
        stringTable.tick = tick;

        uint byteIndex = 0;
        uint bytesRead;

        while (byteIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 2)
            {
                if (fieldNum == 1)
                {
                    stringTable.name = DataParser.ReadProtoString(data, byteIndex, out bytesRead);
                    byteIndex += bytesRead;
                    continue;
                }
                else if (fieldNum == 8)
                {
                    uint dataLength = (uint)DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
                    byteIndex += bytesRead;

                    stringTable.ReadEntries(DataParser.ReadBytes(data, byteIndex, dataLength));
                    byteIndex += dataLength;

                    break;
                }
            }

            if (wireType != 0)
                throw new Exception("StringTable: WireType must be 0");

            int value = DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;

            if (fieldNum == 2) stringTable.maxEntries = value;
            else if (fieldNum == 3) stringTable.numEntries = value;
            else if (fieldNum == 4) stringTable.userDataFixedSize = value != 0;
            else if (fieldNum == 5) stringTable.userDataSize = value;
            else if (fieldNum == 6) stringTable.userDataSizeBits = value;
            else if (fieldNum == 7) stringTable.flags = value;
        }

        return stringTable;
    }
    public static void ParseUpdate(byte[] data, Tick tick)
    {
        uint byteIndex = 0;
        uint bytesRead;

        int tableID = -1;
        int numChangedEntries = 0;
        while (byteIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType == 2 && fieldNum == 3)
            {
                uint length = (uint)DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
                byteIndex += bytesRead;

                StringTable table = tick.demo.stringTables[tableID];
                table.numEntries = numChangedEntries;
                if (table.name == "userinfo" || table.name == "modelprecache" || table.name == "instancebaseline")
                {
                    table.ReadEntries(DataParser.ReadBytes(data, byteIndex, length));
                    byteIndex += length;
                }
                break;
            }

            if (wireType != 0)
                throw new Exception("StringTables: WireType should equal 0");

            int value = DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;
            if (fieldNum == 1) tableID = value;
            else if (fieldNum == 2) numChangedEntries = value;
        }
    }
    private void ReadEntries(byte[] data)
    {
        uint bitIndex = 0;

        if (name == "modelprecache")
            while (tick.demo.modelPrecache.Count < maxEntries)
                tick.demo.modelPrecache.Add(null);

        bool tempBool = DataParser.ReadBit(data, bitIndex);
        bitIndex += 1;
        if (tempBool)
            throw new Exception("StringTable: Unable to decode dictionaries");

        int nTemp = maxEntries;
        byte nEntryBits = 0;
        while ((nTemp >>= 1) != 0)
            ++nEntryBits;

        List<string> history = new List<string>();

        int lastEntry = -1;

        for (int i = 0; i < numEntries; i++)
        {
            int entryIndex = lastEntry + 1;

            tempBool = DataParser.ReadBit(data, bitIndex);
            bitIndex += 1;
            if (!tempBool)
            {
                entryIndex = DataParser.ReadInt(data, bitIndex, nEntryBits);
                bitIndex += nEntryBits;
                //if (DemoParser.prints < 9) Debug.Log("EntryIndex: " + entryIndex);
            }

            lastEntry = entryIndex;

            string entry = "";
            if (entryIndex < 0 || entryIndex >= maxEntries)
                throw new Exception("StringTable: Index out of bounds");

            tempBool = DataParser.ReadBit(data, bitIndex);
            bitIndex += 1;
            if (tempBool)
            {
                bool substringCheck = DataParser.ReadBit(data, bitIndex);
                bitIndex += 1;
                //if (DemoParser.prints < 9) Debug.Log("SubstringCheck: " + substringCheck);

                if (substringCheck)
                {
                    int index = DataParser.ReadInt(data, bitIndex, 5);
                    bitIndex += 5;
                    //if (DemoParser.prints < 9) Debug.Log("Index: " + index);
                    int bytesToCopy = DataParser.ReadInt(data, bitIndex, 5);
                    bitIndex += 5;
                    //if (DemoParser.prints < 9) Debug.Log("BytesToCopy: " + bytesToCopy);

                    //Debug.Log("History[" + index + "]: " + history[index]);
                    entry = history[index].Substring(0, bytesToCopy);
                }

                uint bitsRead;
                string limitedString = DataParser.ReadLimitedString(data, bitIndex, out bitsRead, 1024);
                bitIndex += bitsRead;

                if (limitedString != null) entry += limitedString;
                //Debug.Log("Entry: " + entry);
            }

            if (history.Count > 31) history.RemoveAt(0);

            history.Add(entry);

            byte[] userData = new byte[0];
            tempBool = DataParser.ReadBit(data, bitIndex);
            bitIndex += 1;
            if (tempBool)
            {
                if (userDataFixedSize)
                {
                    //Debug.Log("UserDataFixedSize");
                    userData = DataParser.ReadBits(data, bitIndex, (uint)userDataSizeBits);
                    bitIndex += (uint)userDataSizeBits;
                }
                else
                {
                    //Debug.Log("!UserDataFixedSize");
                    int bytesToRead = DataParser.ReadInt(data, bitIndex, 14);
                    bitIndex += 14;

                    #region BlazingBlace
                    //BytesToRead: 175 58 123 5091 1607 141 75 175 35
                    #endregion
                    //Debug.Log("BitIndex: " + bitIndex + " BytesToRead: " + bytesToRead + " Data.Length: " + data.Length);
                    userData = DataParser.ReadBits(data, bitIndex, (uint)(bytesToRead * 8));
                    //userData = DataParser.ReadBytes(data, bitIndex / 8, (uint)bytesToRead);
                    bitIndex += (uint)(bytesToRead * 8);
                }
            }

            if (userData.Length == 0) break;

            if (name == "userinfo")
            {
                //Debug.Log("UserInfo");
                PlayerInfo playerInfo = PlayerInfo.Parse(userData);

                tick.demo.playerInfo[entryIndex] = playerInfo;
            }
            else if (name == "instancebaseline")
            {
                int classID = int.Parse(entry);
                //Debug.Log("Instancebaseline " + classID);
                tick.demo.instanceBaselines[classID] = userData;
            }
            else if (name == "modelprecache")
            {
                //Debug.Log("ModelPrecache");
                tick.demo.modelPrecache[entryIndex] = entry;
            }
        }
    }
}

public class NetTick
{
    public uint tick;
    public uint hostComputationTime;
    public uint hostComputationTimeStdDeviation;
    public uint hostFrameStartTimeStdDeviation;

    public static NetTick Parse(byte[] data)
    {
        uint byteIndex = 0;
        uint bytesRead;

        NetTick netTick = new NetTick();
        while(byteIndex < data.Length)
        {
            int desc = DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;
            int wireType = desc & 7;
            int fieldNum = desc >> 3;

            if (wireType != 0)
                throw new Exception("NetTick: WireType should be 0");

            uint value = (uint)DataParser.ReadProtoInt(data, byteIndex, out bytesRead);
            byteIndex += bytesRead;

            if (fieldNum == 1) netTick.tick = value;
            else if (fieldNum == 4) netTick.hostComputationTime = value;
            else if (fieldNum == 5) netTick.hostComputationTimeStdDeviation = value;
            else if (fieldNum == 6) netTick.hostFrameStartTimeStdDeviation = value;
        }

        return netTick;
    }
}

public enum EquipmentElement
{
    Unknown = 0,

    //Pistols
    WeaponHKP2000 = 1,
    WeaponGlock = 2,
    WeaponP250 = 3,
    WeaponDEagle = 4,
    WeaponFiveSeven = 5,
    WeaponElite = 6,
    WeaponTec9 = 7,
    WeaponCZ75a = 8,
    WeaponUSP = 9,
    WeaponRevolver = 10,

    //SMGs
    WeaponMP7 = 101,
    WeaponMP9 = 102,
    WeaponBizon = 103,
    WeaponMAC10 = 104,
    WeaponUMP45 = 105,
    WeaponP90 = 106,

    //Heavy
    WeaponSawedoff = 201,
    WeaponNOVA = 202,
    WeaponMag7 = 203,
    WeaponXM1014 = 204,
    WeaponM249 = 205,
    WeaponNegev = 206,

    //Rifle
    WeaponGalilAR = 301,
    WeaponFamas = 302,
    WeaponAK47 = 303,
    WeaponM4A4 = 304,
    WeaponM4A1 = 305,
    WeaponSSG08 = 306,
    WeaponSG556 = 307,
    WeaponAug = 308,
    WeaponAWP = 309,
    WeaponSCAR20 = 310,
    WeaponG3SG1 = 311,

    //Equipment
    WeaponTaser = 401,
    Kevlar = 402,
    Helmet = 403,
    WeaponC4 = 404,
    WeaponKnife = 405,
    DefuseKit = 406,
    World = 407,

    //Grenades
    DecoyGrenade = 501,
    MolotovGrenade = 502,
    IncendiaryGrenade = 503,
    Flashbang = 504,
    SmokeGrenade = 505,
    HEGrenade = 506
}
public enum EquipmentClass
{
    Unknown = 0,
    Pistol = 1,
    SMG = 2,
    Heavy = 3,
    Rifle = 4,
    Equipment = 5,
    Grenade = 6,
}
public enum DemoCommand
{
    Signon = 1, // it's a startup message, process as fast as possible
    Packet, // it's a normal network packet that we stored off
    Synctick, // sync client clock to demo tick
    ConsoleCommand, // Console Command
    UserCommand, // user input command
    DataTables, //  network data tables
    Stop, // end of time.
    CustomData, // a blob of binary data understood by a callback function

    StringTables,
    LastCommand = StringTables, // Last Command
    FirstCommand = Signon, // First Command
};