﻿using Artisan.CraftingLists;
using Artisan.IPC;
using Artisan.RawInformation;
using Dalamud.Interface.Colors;
using Dalamud.Logging;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Artisan.FCWorkshops
{
    internal static class FCWorkshopUI
    {
        internal static uint SelectedProject = 0;
        internal static CompanyCraftSequence CurrentProject => LuminaSheets.WorkshopSequenceSheet[SelectedProject];
        private static string Search = string.Empty;


        internal static void Draw()
        {
            ImGui.TextWrapped($"In this tab, you can browse all the FC Workshop projects in the game. " +
                $"It is broken into 3 main sections. The first is an overview of the full project. " +
                $"The second breaks down each of the parts. " +
                $"The last is each of the phases. " +
                $"In each section, you can click a button to create a crafting list with all you " +
                $"need to craft that particular section.");


            ImGui.Separator();
            string preview = SelectedProject != 0 ? LuminaSheets.ItemSheet[LuminaSheets.WorkshopSequenceSheet[SelectedProject].ResultItem.Row].Name.RawString : "";
            if (ImGui.BeginCombo("###Workshop Project", preview))
            {
                ImGui.Text("Search");
                ImGui.SameLine();
                ImGui.InputText("###ProjectSearch", ref Search, 100);

                if (ImGui.Selectable("", SelectedProject == 0))
                {
                    SelectedProject = 0;
                }

                foreach (var project in LuminaSheets.WorkshopSequenceSheet.Values.Where(x => x.RowId > 0).Where(x => x.ResultItem.Value.Name.RawString.Contains(Search, StringComparison.CurrentCultureIgnoreCase)))
                {
                    bool selected = ImGui.Selectable($"{project.ResultItem.Value.Name.RawString}", project.RowId == SelectedProject);

                    if (selected)
                    {
                        SelectedProject = project.RowId;
                    }
                }

                ImGui.EndCombo();
            }

            if (SelectedProject != 0)
            {
                var project = LuminaSheets.WorkshopSequenceSheet[SelectedProject];

                if (ImGui.CollapsingHeader("Project Information"))
                {
                    if (ImGui.BeginTable($"FCWorkshopProjectContainer", 2, ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn($"###Description", ImGuiTableColumnFlags.WidthFixed);

                        ImGui.TableNextColumn();

                        ImGuiEx.Text($"Selected project:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.ResultItem.Value.Name.RawString}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"Number of parts:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).Count()}");
                        ImGui.TableNextColumn();
                        ImGuiEx.Text($"Total number of phases:");
                        ImGui.TableNextColumn();
                        ImGui.Text($"{project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).Count()}");

                        ImGui.EndTable();
                    }
                    if (ImGui.BeginTable($"###FCWorkshopProjectItemsContainer", RetainerInfo.ATools ? 4 : 3, ImGuiTableFlags.Borders))
                    {
                        ImGui.TableSetupColumn($"Item", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn($"Total Required", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn($"Inventory", ImGuiTableColumnFlags.WidthFixed);
                        if (RetainerInfo.ATools) ImGui.TableSetupColumn($"Retainers", ImGuiTableColumnFlags.WidthFixed);

                        ImGui.TableHeadersRow();

                        Dictionary<uint, int> TotalItems = new Dictionary<uint, int>();
                        foreach (var item in project.CompanyCraftPart.Where(x => x.Row > 0).SelectMany(x => x.Value.CompanyCraftProcess).Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                        {
                            if (TotalItems.ContainsKey(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row))
                                TotalItems[LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row] += item.Sum(x => x.SetQuantity * x.SetsRequired);
                            else
                                TotalItems.TryAdd(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row, item.Sum(x => x.SetQuantity * x.SetsRequired));
                        }

                        foreach (var item in TotalItems)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{LuminaSheets.ItemSheet[item.Key].Name.RawString}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{item.Value}");
                            ImGui.TableNextColumn();
                            int invCount = CraftingListUI.NumberOfIngredient(LuminaSheets.ItemSheet[item.Key].RowId);
                            ImGui.Text($"{invCount}");
                            bool hasEnoughInInv = invCount >= item.Value;
                            if (hasEnoughInInv)
                            {
                                var color = ImGuiColors.HealerGreen;
                                color.W -= 0.3f;
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                            }
                            if (RetainerInfo.ATools)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text($"{RetainerInfo.GetRetainerItemCount(LuminaSheets.ItemSheet[item.Key].RowId)}");

                                bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(LuminaSheets.ItemSheet[item.Key].RowId) >= item.Value);
                                if (!hasEnoughInInv && hasEnoughWithRetainer)
                                {
                                    var color = ImGuiColors.DalamudOrange;
                                    color.W -= 0.6f;
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                }
                            }

                        }

                        ImGui.EndTable();
                    }
                    if (ImGui.Button($"Create Crafting List for this Project", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        CreateProjectList(project, false);
                        Notify.Success("FC Workshop List Created");
                    }

                    if (ImGui.Button($"Create Crafting List for this Project (Including pre-crafts)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                    {
                        CreateProjectList(project, true);
                        Notify.Success("FC Workshop List Created");
                    }
                }
                if (ImGui.CollapsingHeader("Project Parts"))
                {
                    ImGui.Indent();
                    string partNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        partNum = part.CompanyCraftType.Value.Name.RawString;
                        if (ImGui.CollapsingHeader($"{partNum}"))
                        {
                            if (ImGui.BeginTable($"FCWorkshopPartsContainer###{part.RowId}", 2, ImGuiTableFlags.None))
                            {
                                ImGui.TableSetupColumn($"###PartType{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"###Phases{part.RowId}", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableNextColumn();

                                ImGuiEx.Text($"Part Type:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftType.Value.Name.RawString}");
                                ImGui.TableNextColumn();
                                ImGuiEx.Text($"Number of phases:");
                                ImGui.TableNextColumn();
                                ImGui.Text($"{part.CompanyCraftProcess.Where(x => x.Row > 0).Count()}");
                                ImGui.TableNextColumn();

                                ImGui.EndTable();
                            }
                            if (ImGui.BeginTable($"###FCWorkshopPartItemsContainer{part.RowId}", RetainerInfo.ATools ? 4 : 3, ImGuiTableFlags.Borders))
                            {
                                ImGui.TableSetupColumn($"Item", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"Total Required", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableSetupColumn($"Inventory", ImGuiTableColumnFlags.WidthFixed);
                                if (RetainerInfo.ATools) ImGui.TableSetupColumn($"Retainers", ImGuiTableColumnFlags.WidthFixed);
                                ImGui.TableHeadersRow();

                                Dictionary<uint, int> TotalItems = new Dictionary<uint, int>();
                                foreach (var item in part.CompanyCraftProcess.Where(x => x.Row > 0).SelectMany(x => x.Value.UnkData0).Where(x => x.SupplyItem > 0).GroupBy(x => x.SupplyItem))
                                {
                                    if (TotalItems.ContainsKey(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row))
                                        TotalItems[LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row] += item.Sum(x => x.SetQuantity * x.SetsRequired);
                                    else
                                        TotalItems.TryAdd(LuminaSheets.WorkshopSupplyItemSheet[item.Select(x => x.SupplyItem).First()].Item.Row, item.Sum(x => x.SetQuantity * x.SetsRequired));

                                }

                                foreach (var item in TotalItems) 
                                { 
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{LuminaSheets.ItemSheet[item.Key].Name.RawString}");
                                    ImGui.TableNextColumn();
                                    ImGui.Text($"{item.Value}");
                                    ImGui.TableNextColumn();
                                    int invCount = CraftingListUI.NumberOfIngredient(item.Key);
                                    ImGui.Text($"{invCount}");
                                    bool hasEnoughInInv = invCount >= item.Value;
                                    if (hasEnoughInInv)
                                    {
                                        var color = ImGuiColors.HealerGreen;
                                        color.W -= 0.3f;
                                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                    }
                                    if (RetainerInfo.ATools)
                                    {
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{RetainerInfo.GetRetainerItemCount(item.Key)}");

                                        bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(item.Key)) >= item.Value;
                                        if (!hasEnoughInInv && hasEnoughWithRetainer)
                                        {
                                            var color = ImGuiColors.DalamudOrange;
                                            color.W -= 0.6f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                    }
                                }

                                ImGui.EndTable();
                            }
                            if (ImGui.Button($"Create Crafting List for this Part", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                CreatePartList(part, partNum, false);
                                Notify.Success("FC Workshop List Created");
                            }

                            if (ImGui.Button($"Create Crafting List for this Part (Including pre-crafts)", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                            {
                                CreatePartList(part, partNum, true);
                                Notify.Success("FC Workshop List Created");
                            }
                        }
                    }
                    ImGui.Unindent();
                }

                if (ImGui.CollapsingHeader("Project Phases"))
                {
                    string pNum = "";
                    foreach (var part in project.CompanyCraftPart.Where(x => x.Row > 0).Select(x => x.Value))
                    {
                        ImGui.Indent();
                        int phaseNum = 1;
                        pNum = part.CompanyCraftType.Value.Name.RawString;
                        foreach (var phase in part.CompanyCraftProcess.Where(x => x.Row > 0))
                        {
                            if (ImGui.CollapsingHeader($"{pNum} - Phase {phaseNum}"))
                            {
                                if (ImGui.BeginTable($"###FCWorkshopPhaseContainer{phase.Row}", RetainerInfo.ATools ? 6 : 5, ImGuiTableFlags.Borders))
                                {
                                    ImGui.TableSetupColumn($"Item", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"Set Quantity", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"Sets Required", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"Total Required", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableSetupColumn($"Inventory", ImGuiTableColumnFlags.WidthFixed);
                                    if (RetainerInfo.ATools) ImGui.TableSetupColumn($"Retainers", ImGuiTableColumnFlags.WidthFixed);
                                    ImGui.TableHeadersRow();

                                    foreach (var item in phase.Value.UnkData0.Where(x => x.SupplyItem > 0))
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.Name.RawString}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetQuantity}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired}");
                                        ImGui.TableNextColumn();
                                        ImGui.Text($"{item.SetsRequired * item.SetQuantity}");
                                        ImGui.TableNextColumn();
                                        int invCount = CraftingListUI.NumberOfIngredient(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row);
                                        ImGui.Text($"{invCount}");
                                        bool hasEnoughInInv = invCount >= (item.SetQuantity * item.SetsRequired);
                                        if (hasEnoughInInv)
                                        {
                                            var color = ImGuiColors.HealerGreen;
                                            color.W -= 0.3f;
                                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                        }
                                        if (RetainerInfo.ATools)
                                        {
                                            ImGui.TableNextColumn();
                                            ImGui.Text($"{RetainerInfo.GetRetainerItemCount(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row)}");

                                            bool hasEnoughWithRetainer = (invCount + RetainerInfo.GetRetainerItemCount(LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Row)) >= (item.SetQuantity * item.SetsRequired);
                                            if (!hasEnoughInInv && hasEnoughWithRetainer)
                                            {
                                                var color = ImGuiColors.DalamudOrange;
                                                color.W -= 0.6f;
                                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.ColorConvertFloat4ToU32(color));
                                            }
                                        }

                                    }

                                    ImGui.EndTable();
                                }
                                if (ImGui.Button($"Create Crafting List for this Phase###PhaseButton{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    CreatePhaseList(phase.Value!, pNum, phaseNum, false);
                                    Notify.Success("FC Workshop List Created");
                                }

                                if (ImGui.Button($"Create Crafting List for this Phase (Including pre-crafts)###PhaseButtonPC{phaseNum}", new Vector2(ImGui.GetContentRegionAvail().X, 24f.Scale())))
                                {
                                    CreatePhaseList(phase.Value!, pNum, phaseNum, true);
                                    Notify.Success("FC Workshop List Created");
                                }

                            }
                            phaseNum++;
                        }
                        ImGui.Unindent();
                    }

                }
            }
        }

        private static void CreatePartList(CompanyCraftPart value, string partNum, bool includePrecraft, CraftingList existingList = null)
        {
            if (existingList == null)
            {
                existingList = new CraftingList();
                existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString} - Part {partNum}";
                existingList.SetID();
                existingList.Save(true);
            }

            var phaseNum = 1;
            foreach (var phase in value.CompanyCraftProcess.Where(x => x.Row > 0))
            {
                CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                phaseNum++;
            }
        }

        private static void CreateProjectList(CompanyCraftSequence value, bool includePrecraft)
        {
            CraftingList existingList = new CraftingList();
            existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString}";
            existingList.SetID();
            existingList.Save(true);

            foreach (var part in value.CompanyCraftPart.Where(x => x.Row > 0))
            {
                string partNum = part.Value.CompanyCraftType.Value.Name.RawString;
                var phaseNum = 1;
                foreach (var phase in part.Value.CompanyCraftProcess.Where(x => x.Row > 0))
                {
                    CreatePhaseList(phase.Value!, partNum, phaseNum, includePrecraft, existingList);
                    phaseNum++;
                }

            }


        }

        public static void CreatePhaseList(CompanyCraftProcess value, string partNum, int phaseNum, bool includePrecraft, CraftingList existingList = null, CompanyCraftSequence projectOverride = null)
        {
            if (existingList == null)
            {
                existingList = new CraftingList();
                if (projectOverride != null)
                {
                    existingList.Name = $"{projectOverride.ResultItem.Value.Name.RawString} - {partNum}, Phase {phaseNum}";
                }
                else
                {
                    existingList.Name = $"{CurrentProject.ResultItem.Value.Name.RawString} - {partNum}, Phase {phaseNum}";
                }
                existingList.SetID();
                existingList.Save(true);
            }

            foreach (var item in value.UnkData0.Where(x => x.SupplyItem > 0))
            {
                var timesToAdd = item.SetsRequired * item.SetQuantity;
                var supplyItemID = LuminaSheets.WorkshopSupplyItemSheet[item.SupplyItem].Item.Value.RowId;
                if (LuminaSheets.RecipeSheet.Values.Any(x => x.ItemResult.Row == supplyItemID))
                {
                    var recipeID = LuminaSheets.RecipeSheet.Values.First(x => x.ItemResult.Row == supplyItemID);
                    if (includePrecraft)
                    {
                        PluginLog.Debug($"I want to add {recipeID.ItemResult.Value.Name.RawString} {timesToAdd} times");
                        CraftingListUI.AddAllSubcrafts(recipeID, existingList, timesToAdd);
                    }

                    for (int i = 1; i <= timesToAdd / recipeID.AmountResult; i++)
                    {
                        if (existingList.Items.IndexOf(recipeID.RowId) == -1)
                        {
                            existingList.Items.Add(recipeID.RowId);
                        }
                        else
                        {
                            var indexOfLast = existingList.Items.IndexOf(recipeID.RowId);
                            existingList.Items.Insert(indexOfLast, recipeID.RowId);
                        }
                    }

                }
            }
            P.Config.Save();

        }
    }
}
