// Role:
// Serializable data for one resident line used by district registry readables.
// Usage:
// Stored inside DistrictRegistry assets; each record can later be linked to rooms,
// lineages, objects, or other readables without changing the book UI.
// Responsibilities:
// Keep stable resident identity, birth year, initial housing, dated events, and
// optional investigation hooks. The legacy "last event" fields remain as a
// fallback for old or partially migrated assets.
// Dependencies:
// Plain Unity serialization, TemporalAgeUtility, and Item readable pages through
// DistrictRegistry.
// Precautions:
// Keep IDs stable once records are referenced by rooms, saves, or other assets.
// Never expose future events directly: page generation asks each record for the
// latest event visible at the consulted temporal year.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Administrative category of an important event visible in a resident register.
/// Numeric values 0-7 are kept compatible with the first registry assets.
/// </summary>
public enum ResidentRecordEventType
{
    Unknown = 0,
    Death = 1,
    Relocation = 2,
    MoveDistrict = 2,
    Disappearance = 3,
    StruckOutName = 4,
    RoomReassigned = 5,
    NoRecordedEvent = 6,
    IncompleteStatus = 7,
    Birth = 8,
    MoveRoom = 9,
    Note = 10
}

/// <summary>
/// Coarse bucket used by validation and authoring reports.
/// </summary>
public enum ResidentRecordEventGroup
{
    Ordinary = 0,
    Relocation = 1,
    Anomaly = 2
}

/// <summary>
/// One dated administrative event for a resident.
/// </summary>
[Serializable]
public class ResidentEvent
{
    [Header("Timing")]
    /// <summary>Administrative year when this event becomes visible in the register.</summary>
    public int year;
    /// <summary>Type used for display, filtering, and validation.</summary>
    public ResidentRecordEventType eventType = ResidentRecordEventType.Note;

    [Header("Place")]
    /// <summary>District ID after this event, when relevant.</summary>
    public string districtId;
    /// <summary>Readable district name after this event, when relevant.</summary>
    public string districtName;
    /// <summary>Chamber or housing number after this event, when relevant.</summary>
    public string habitationNumber;

    [Header("Readable Detail")]
    /// <summary>Destination district for MoveDistrict events, if different from districtName.</summary>
    public string destinationDistrict;
    /// <summary>Cause for deaths, disappearances, administrative unknowns, or room events.</summary>
    public string cause;
    /// <summary>Optional text that replaces the default generated event label.</summary>
    public string readableTextOverride;
    /// <summary>Internal or marginal note. It is not shown unless no better text exists.</summary>
    [TextArea]
    public string note;
    /// <summary>True when this event causes the displayed name to be marked as struck out.</summary>
    public bool isNameStruckOut;

    [Header("Future Links")]
    /// <summary>Optional room ID for investigation links created by this event.</summary>
    public string relatedRoomId;
    /// <summary>Optional object ID for evidence connected to this event.</summary>
    public string relatedObjectId;
    /// <summary>Optional readable ID connected to this event.</summary>
    public string relatedReadableId;

    public bool HasRoom => !string.IsNullOrWhiteSpace(habitationNumber);

    public bool IsImportantForLastEvent()
    {
        return eventType != ResidentRecordEventType.Birth
            && eventType != ResidentRecordEventType.NoRecordedEvent;
    }

    public ResidentRecordEventGroup GetEventGroup()
    {
        switch (eventType)
        {
            case ResidentRecordEventType.Relocation:
                return ResidentRecordEventGroup.Relocation;

            case ResidentRecordEventType.Disappearance:
            case ResidentRecordEventType.StruckOutName:
            case ResidentRecordEventType.IncompleteStatus:
            case ResidentRecordEventType.Unknown:
                return ResidentRecordEventGroup.Anomaly;

            case ResidentRecordEventType.Birth:
            case ResidentRecordEventType.Death:
            case ResidentRecordEventType.RoomReassigned:
            case ResidentRecordEventType.MoveRoom:
            case ResidentRecordEventType.Note:
            case ResidentRecordEventType.NoRecordedEvent:
            default:
                return ResidentRecordEventGroup.Ordinary;
        }
    }

    public string BuildReadableEventText()
    {
        if (!string.IsNullOrWhiteSpace(readableTextOverride))
        {
            return readableTextOverride.Trim();
        }

        string detail = !string.IsNullOrWhiteSpace(cause) ? cause.Trim() : string.Empty;
        switch (eventType)
        {
            case ResidentRecordEventType.Death:
                return string.IsNullOrWhiteSpace(detail) ? "Décès" : $"Décès : {detail}";

            case ResidentRecordEventType.Relocation:
                string destination = !string.IsNullOrWhiteSpace(destinationDistrict)
                    ? destinationDistrict.Trim()
                    : districtName;
                return string.IsNullOrWhiteSpace(destination) ? "Déplacé" : $"Déplacé vers {destination}";

            case ResidentRecordEventType.Disappearance:
                return string.IsNullOrWhiteSpace(detail) ? "Disparu hors registre" : $"Disparu hors registre : {detail}";

            case ResidentRecordEventType.StruckOutName:
                return "Nom rayé";

            case ResidentRecordEventType.RoomReassigned:
                return "Chambre réattribuée";

            case ResidentRecordEventType.IncompleteStatus:
                return "Statut incomplet";

            case ResidentRecordEventType.MoveRoom:
                return HasRoom ? $"Déplacé vers chambre {habitationNumber.Trim()}" : "Déplacé de chambre";

            case ResidentRecordEventType.Note:
                return !string.IsNullOrWhiteSpace(note) ? note.Trim() : "Note marginale";

            case ResidentRecordEventType.Unknown:
                return string.IsNullOrWhiteSpace(detail) ? "Affectation inconnue" : $"Affectation inconnue : {detail}";

            case ResidentRecordEventType.Birth:
                return "Naissance enregistrée";

            case ResidentRecordEventType.NoRecordedEvent:
            default:
                return "Aucun événement enregistré";
        }
    }
}

/// <summary>
/// One resident line in a district registry.
/// </summary>
[Serializable]
public class ResidentRecord
{
    [Header("Identity")]
    /// <summary>Stable resident ID, for example RES_LUNE_0001.</summary>
    public string residentId;
    /// <summary>First name as written in the surviving register.</summary>
    public string firstName;
    /// <summary>Family name as written in the surviving register.</summary>
    public string lastName;

    [Header("Life and Housing")]
    /// <summary>Birth year used for sorting, filtering, and section headers.</summary>
    public int birthYear;
    /// <summary>Stable district ID where the record originates.</summary>
    public string initialDistrictId;
    /// <summary>Readable district name where the record originates.</summary>
    public string initialDistrictName;
    /// <summary>Initial chamber or housing number shown before later room events.</summary>
    public string habitationNumber;

    [Header("Dated Events")]
    /// <summary>
    /// Chronological administrative history. Birth events are useful for tooling,
    /// but the readable line only displays the latest non-birth event at the
    /// consulted year.
    /// </summary>
    public List<ResidentEvent> events = new List<ResidentEvent>();

    [Header("Legacy Last Important Event")]
    /// <summary>Administrative type of the last visible event for old assets.</summary>
    public ResidentRecordEventType lastEventType = ResidentRecordEventType.NoRecordedEvent;
    /// <summary>Text displayed after the name and chamber for old assets.</summary>
    public string lastEventText = "Aucun evenement enregistre";
    /// <summary>Optional event year for old assets, 0 when the register gives no date.</summary>
    public int eventYear;
    /// <summary>Destination district for legacy relocation records.</summary>
    public string destinationDistrict;
    /// <summary>Legacy struck-out marker. Prefer a dated StruckOutName event.</summary>
    public bool isNameStruckOut;

    [Header("Future Links")]
    /// <summary>Optional family ID used by later lineage tools.</summary>
    public string familyId;
    /// <summary>Optional lineage ID used by later lineage tools.</summary>
    public string lineageId;
    /// <summary>Optional room ID for linking to an explorable chamber.</summary>
    public string relatedRoomId;
    /// <summary>Optional object ID for transgenerational evidence.</summary>
    public string relatedObjectId;
    /// <summary>Optional readable ID connected to this resident.</summary>
    public string relatedReadableId;
    /// <summary>Freeform tags for future filtering and investigation tools.</summary>
    public List<string> narrativeTags = new List<string>();

    [Header("Design Notes")]
    /// <summary>Internal note not shown in the generated readable page.</summary>
    [TextArea]
    public string internalNote;

    public string DisplayName
    {
        get
        {
            string first = string.IsNullOrWhiteSpace(firstName) ? string.Empty : firstName.Trim();
            string last = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName.Trim();
            if (string.IsNullOrEmpty(first))
            {
                return last;
            }

            if (string.IsNullOrEmpty(last))
            {
                return first;
            }

            return $"{first} {last}";
        }
    }

    public string SortName
    {
        get
        {
            string last = string.IsNullOrWhiteSpace(lastName) ? string.Empty : lastName.Trim();
            string first = string.IsNullOrWhiteSpace(firstName) ? string.Empty : firstName.Trim();
            return $"{last}|{first}";
        }
    }

    public bool IsVisibleAtYear(int consultedYear)
    {
        return birthYear <= ClampYear(consultedYear);
    }

    public ResidentRecordEventGroup GetEventGroup()
    {
        return GetEventGroup(TemporalAgeUtility.MaxYear);
    }

    public ResidentRecordEventGroup GetEventGroup(int consultedYear)
    {
        ResidentEvent latestEvent = GetLatestImportantEventForYear(consultedYear);
        if (latestEvent != null)
        {
            return latestEvent.GetEventGroup();
        }

        if (HasDatedEvents())
        {
            return ResidentRecordEventGroup.Ordinary;
        }

        switch (lastEventType)
        {
            case ResidentRecordEventType.Relocation:
                return ResidentRecordEventGroup.Relocation;

            case ResidentRecordEventType.Disappearance:
            case ResidentRecordEventType.StruckOutName:
            case ResidentRecordEventType.IncompleteStatus:
            case ResidentRecordEventType.Unknown:
                return ResidentRecordEventGroup.Anomaly;

            case ResidentRecordEventType.Death:
            case ResidentRecordEventType.RoomReassigned:
            case ResidentRecordEventType.MoveRoom:
            case ResidentRecordEventType.Note:
            case ResidentRecordEventType.Birth:
            case ResidentRecordEventType.NoRecordedEvent:
            default:
                return ResidentRecordEventGroup.Ordinary;
        }
    }

    public string BuildReadableLine()
    {
        return BuildReadableLine(TemporalAgeUtility.MaxYear);
    }

    public string BuildReadableLine(int consultedYear)
    {
        string name = DisplayName;
        if (IsNameStruckOutAtYear(consultedYear))
        {
            name = $"[Nom rayé] {name}";
        }

        string resolvedRoom = ResolveHabitationNumberAtYear(consultedYear);
        string room = string.IsNullOrWhiteSpace(resolvedRoom)
            ? "Chambre non reportée"
            : $"Chambre {resolvedRoom.Trim()}";
        string eventText = ResolveReadableEventTextAtYear(consultedYear);

        return $"- {name} — {room} — {eventText}";
    }

    public ResidentEvent GetLatestImportantEventForYear(int consultedYear)
    {
        if (!HasDatedEvents())
        {
            return null;
        }

        int clampedYear = ClampYear(consultedYear);
        ResidentEvent latest = null;
        for (int i = 0; i < events.Count; i++)
        {
            ResidentEvent candidate = events[i];
            if (candidate == null || candidate.year > clampedYear || !candidate.IsImportantForLastEvent())
            {
                continue;
            }

            if (latest == null || CompareEvents(candidate, latest) > 0)
            {
                latest = candidate;
            }
        }

        return latest;
    }

    public string ResolveHabitationNumberAtYear(int consultedYear)
    {
        string room = habitationNumber;
        if (!HasDatedEvents())
        {
            return room;
        }

        int clampedYear = ClampYear(consultedYear);
        for (int i = 0; i < events.Count; i++)
        {
            ResidentEvent candidate = events[i];
            if (candidate == null || candidate.year > clampedYear || !candidate.HasRoom)
            {
                continue;
            }

            room = candidate.habitationNumber;
        }

        return room;
    }

    public bool IsNameStruckOutAtYear(int consultedYear)
    {
        int clampedYear = ClampYear(consultedYear);
        if (HasDatedEvents())
        {
            for (int i = 0; i < events.Count; i++)
            {
                ResidentEvent candidate = events[i];
                if (candidate == null || candidate.year > clampedYear)
                {
                    continue;
                }

                if (candidate.isNameStruckOut || candidate.eventType == ResidentRecordEventType.StruckOutName)
                {
                    return true;
                }
            }

            return false;
        }

        if (!isNameStruckOut && lastEventType != ResidentRecordEventType.StruckOutName)
        {
            return false;
        }

        return eventYear <= 0 || eventYear <= clampedYear;
    }

    private string ResolveReadableEventTextAtYear(int consultedYear)
    {
        ResidentEvent latestEvent = GetLatestImportantEventForYear(consultedYear);
        if (latestEvent != null)
        {
            return latestEvent.BuildReadableEventText();
        }

        if (HasDatedEvents())
        {
            return "Aucun événement enregistré";
        }

        if (eventYear > 0 && eventYear > ClampYear(consultedYear))
        {
            return "Aucun événement enregistré";
        }

        return string.IsNullOrWhiteSpace(lastEventText)
            ? "Statut incomplet"
            : lastEventText.Trim();
    }

    private bool HasDatedEvents()
    {
        return events != null && events.Count > 0;
    }

    private static int CompareEvents(ResidentEvent left, ResidentEvent right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return -1;
        }

        if (right == null)
        {
            return 1;
        }

        int yearCompare = left.year.CompareTo(right.year);
        if (yearCompare != 0)
        {
            return yearCompare;
        }

        return left.eventType.CompareTo(right.eventType);
    }

    private static int ClampYear(int year)
    {
        return Mathf.Clamp(year, TemporalAgeUtility.MinYear, TemporalAgeUtility.MaxYear);
    }
}
