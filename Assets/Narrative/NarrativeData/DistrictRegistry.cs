// Role:
// Data source for one district resident registry and its generated readable pages.
// Usage:
// Author residents here, assign a readable Item, then rebuild static pages from
// the editor tool. At runtime DistrictRegistryReadable can regenerate pages for
// the currently consulted temporal year.
// Responsibilities:
// Sort residents by birth year then name, hide residents not born yet, choose
// the latest event that has already happened, and emit Item.ReadablePage content
// for the existing BookPanel UI.
// Dependencies:
// ResidentRecord, ResidentEvent, Item readable pages, TemporalReadableMetadata.
// Precautions:
// This asset owns data. The Item owns display pages. Do not put UI references
// here; runtime components decide which temporal year is being consulted.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// District-level registry data that can generate a readable book Item.
/// </summary>
[CreateAssetMenu(fileName = "DistrictRegistry", menuName = "Scriptable Objects/Narrative/District Registry")]
public class DistrictRegistry : ScriptableObject
{
    [Header("Identity")]
    /// <summary>Stable registry ID, for example REG_LUNE_PLEINE.</summary>
    public string registryId;
    /// <summary>Stable district ID, for example DIST_LUNE_PLEINE.</summary>
    public string districtId;
    /// <summary>District name displayed in generated pages.</summary>
    public string districtName;
    /// <summary>Administrative title displayed on the first page.</summary>
    public string registryTitle;

    [Header("Readable Item")]
    /// <summary>Existing Item asset that will receive generated book pages.</summary>
    public Item readableItem;
    /// <summary>Inventory description written to the readable Item when rebuilt.</summary>
    [TextArea]
    public string itemDescription;
    /// <summary>Optional prose shown on the cover page before resident entries.</summary>
    [TextArea(3, 8)]
    public string coverNote;

    [Header("Pagination")]
    /// <summary>How many birth years each administrative section covers.</summary>
    [Min(1)]
    public int yearSectionSpan = 40;
    /// <summary>Maximum resident lines per generated page.</summary>
    [Min(1)]
    public int maxEntriesPerPage = 6;

    [Header("Residents")]
    /// <summary>Raw resident records. Generated pages sort and filter this list automatically.</summary>
    public List<ResidentRecord> residents = new List<ResidentRecord>();

    public IReadOnlyList<ResidentRecord> Residents => residents;

    public List<ResidentRecord> GetSortedResidents()
    {
        List<ResidentRecord> sorted = new List<ResidentRecord>();
        if (residents != null)
        {
            for (int i = 0; i < residents.Count; i++)
            {
                if (residents[i] != null)
                {
                    sorted.Add(residents[i]);
                }
            }
        }

        sorted.Sort(CompareResidents);
        return sorted;
    }

    public List<ResidentRecord> GetVisibleSortedResidents(int consultedYear)
    {
        int year = ClampYear(consultedYear);
        List<ResidentRecord> sorted = GetSortedResidents();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            ResidentRecord record = sorted[i];
            if (record == null || !record.IsVisibleAtYear(year))
            {
                sorted.RemoveAt(i);
            }
        }

        return sorted;
    }

    /// <summary>
    /// Builds the complete final registry. This preserves the previous editor
    /// workflow and gives non-temporal readables their full Age666 content.
    /// </summary>
    public List<Item.ReadablePage> BuildReadablePages()
    {
        return BuildReadablePagesForYear(TemporalAgeUtility.MaxYear);
    }

    public List<Item.ReadablePage> BuildReadablePagesForAge(TemporalAge consultedAge)
    {
        return BuildReadablePagesForYear(TemporalAgeUtility.AgeToInt(consultedAge));
    }

    public List<Item.ReadablePage> BuildReadablePagesForYear(int consultedYear)
    {
        int year = ClampYear(consultedYear);
        List<Item.ReadablePage> pages = new List<Item.ReadablePage>();
        List<ResidentRecord> visibleResidents = GetVisibleSortedResidents(year);

        pages.Add(new Item.ReadablePage { text = BuildCoverPage(visibleResidents, year) });
        if (visibleResidents.Count == 0)
        {
            return pages;
        }

        int span = Mathf.Max(1, yearSectionSpan);
        int entriesPerPage = Mathf.Max(1, maxEntriesPerPage);
        int index = 0;
        while (index < visibleResidents.Count)
        {
            int sectionStart = GetSectionStart(visibleResidents[index].birthYear, span);
            int sectionEnd = sectionStart + span - 1;
            List<ResidentRecord> section = new List<ResidentRecord>();
            while (index < visibleResidents.Count)
            {
                ResidentRecord record = visibleResidents[index];
                int recordSectionStart = GetSectionStart(record.birthYear, span);
                if (recordSectionStart != sectionStart)
                {
                    break;
                }

                section.Add(record);
                index++;
            }

            AddSectionPages(pages, section, sectionStart, sectionEnd, entriesPerPage, year);
        }

        return pages;
    }

    public void ApplyToReadableItem()
    {
        ApplyToReadableItemForYear(TemporalAgeUtility.MaxYear);
    }

    public void ApplyToReadableItemForYear(int consultedYear)
    {
        ApplyToReadableItem(readableItem, consultedYear);
    }

    public void ApplyToReadableItem(Item targetItem, int consultedYear)
    {
        if (targetItem == null)
        {
            return;
        }

        targetItem.itemId = registryId;
        targetItem.itemName = string.IsNullOrWhiteSpace(registryTitle)
            ? districtName
            : registryTitle;
        targetItem.description = itemDescription ?? string.Empty;
        targetItem.readableKind = Item.ReadableKind.Book;
        targetItem.parchmentText = string.Empty;
        targetItem.bookPages = BuildReadablePagesForYear(consultedYear);
        targetItem.useRandomSentences = false;
        targetItem.candidateSentences = new List<Item.ReadableSentence>();
        targetItem.generatedSentenceCount = 1;
        targetItem.readableContentId = registryId;
        targetItem.canUse = true;
        targetItem.temporalDistrictRegistry = this;
        targetItem.refreshTemporalDistrictRegistryOnRead = true;
        targetItem.readableMetadata.enabled = true;
        targetItem.readableMetadata.associatedAge = TemporalAgeUtility.IntToAge(consultedYear);
        targetItem.readableMetadata.district = districtName;
        targetItem.readableMetadata.room = string.Empty;
        targetItem.readableMetadata.lineageId = string.Empty;
        targetItem.readableMetadata.religiousCurrent = ReligiousCurrent.Mediators;
        targetItem.readableMetadata.revelationLevel = NarrativeRevelationLevel.Routine;
        targetItem.readableMetadata.narrativeTags = new List<string>
        {
            "registre",
            "habitants",
            "registre-temporel",
            districtId,
            registryId
        };
    }

    public void GetEventGroupCounts(out int ordinary, out int relocations, out int anomalies)
    {
        GetEventGroupCountsForYear(TemporalAgeUtility.MaxYear, out ordinary, out relocations, out anomalies);
    }

    public void GetEventGroupCountsForYear(int consultedYear, out int ordinary, out int relocations, out int anomalies)
    {
        ordinary = 0;
        relocations = 0;
        anomalies = 0;
        List<ResidentRecord> visibleResidents = GetVisibleSortedResidents(consultedYear);
        for (int i = 0; i < visibleResidents.Count; i++)
        {
            ResidentRecord record = visibleResidents[i];
            if (record == null)
            {
                continue;
            }

            switch (record.GetEventGroup(consultedYear))
            {
                case ResidentRecordEventGroup.Relocation:
                    relocations++;
                    break;

                case ResidentRecordEventGroup.Anomaly:
                    anomalies++;
                    break;

                case ResidentRecordEventGroup.Ordinary:
                default:
                    ordinary++;
                    break;
            }
        }
    }

    public List<string> FindDuplicateResidentIds()
    {
        List<string> duplicates = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        if (residents == null)
        {
            return duplicates;
        }

        for (int i = 0; i < residents.Count; i++)
        {
            string id = residents[i] != null ? residents[i].residentId : string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!seen.Add(id) && !duplicates.Contains(id))
            {
                duplicates.Add(id);
            }
        }

        return duplicates;
    }

    private string BuildCoverPage(List<ResidentRecord> visibleResidents, int consultedYear)
    {
        GetEventGroupCountsForYear(consultedYear, out int ordinary, out int relocations, out int anomalies);
        int minYear = visibleResidents.Count > 0 ? visibleResidents[0].birthYear : 0;
        int maxYear = visibleResidents.Count > 0 ? visibleResidents[visibleResidents.Count - 1].birthYear : 0;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrWhiteSpace(registryTitle) ? districtName : registryTitle);
        builder.AppendLine(string.IsNullOrWhiteSpace(districtName) ? "Quartier non precise" : districtName);
        builder.AppendLine($"Consultation temporelle: An {ClampYear(consultedYear)}");
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(coverNote))
        {
            builder.AppendLine(coverNote.Trim());
            builder.AppendLine();
        }

        if (visibleResidents.Count > 0)
        {
            builder.AppendLine($"Naissances lisibles: An {minYear} à An {maxYear}");
        }
        else
        {
            builder.AppendLine("Naissances lisibles: aucune entree a cet age");
        }

        builder.AppendLine($"Entrées lisibles: {visibleResidents.Count}");
        builder.AppendLine($"Ordinaires: {ordinary}  Déplacements: {relocations}  Anomalies: {anomalies}");
        builder.AppendLine();
        builder.AppendLine("Classement: année de naissance, puis nom de famille.");
        builder.AppendLine("Les ratures et corrections n'apparaissent qu'a partir de leur annee.");
        return builder.ToString().TrimEnd();
    }

    private void AddSectionPages(
        List<Item.ReadablePage> pages,
        List<ResidentRecord> section,
        int sectionStart,
        int sectionEnd,
        int entriesPerPage,
        int consultedYear)
    {
        int pageEntryCount = 0;
        StringBuilder builder = null;
        int currentYear = int.MinValue;
        int sectionPage = 0;

        for (int i = 0; i < section.Count; i++)
        {
            ResidentRecord record = section[i];
            if (builder == null || pageEntryCount >= entriesPerPage)
            {
                FlushPage(pages, builder);
                sectionPage++;
                builder = CreateSectionHeader(sectionStart, sectionEnd, sectionPage, consultedYear);
                pageEntryCount = 0;
                currentYear = int.MinValue;
            }

            if (record.birthYear != currentYear)
            {
                if (pageEntryCount > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine($"An {record.birthYear}");
                currentYear = record.birthYear;
            }

            builder.AppendLine(record.BuildReadableLine(consultedYear));
            pageEntryCount++;
        }

        FlushPage(pages, builder);
    }

    private StringBuilder CreateSectionHeader(int sectionStart, int sectionEnd, int sectionPage, int consultedYear)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.IsNullOrWhiteSpace(districtName) ? "Registre de quartier" : districtName);
        builder.AppendLine($"Consultation An {ClampYear(consultedYear)}");
        builder.AppendLine($"Naissances An {sectionStart} - An {sectionEnd}" + (sectionPage > 1 ? " (suite)" : string.Empty));
        builder.AppendLine();
        return builder;
    }

    private static void FlushPage(List<Item.ReadablePage> pages, StringBuilder builder)
    {
        if (builder == null)
        {
            return;
        }

        string text = builder.ToString().TrimEnd();
        if (!string.IsNullOrWhiteSpace(text))
        {
            pages.Add(new Item.ReadablePage { text = text });
        }
    }

    private static int CompareResidents(ResidentRecord left, ResidentRecord right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        int yearCompare = left.birthYear.CompareTo(right.birthYear);
        if (yearCompare != 0)
        {
            return yearCompare;
        }

        int nameCompare = string.Compare(left.SortName, right.SortName, StringComparison.OrdinalIgnoreCase);
        if (nameCompare != 0)
        {
            return nameCompare;
        }

        return string.Compare(left.residentId, right.residentId, StringComparison.Ordinal);
    }

    private static int GetSectionStart(int year, int span)
    {
        if (span <= 0)
        {
            return year;
        }

        return Mathf.FloorToInt(year / (float)span) * span;
    }

    private static int ClampYear(int year)
    {
        return Mathf.Clamp(year, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
    }
}
