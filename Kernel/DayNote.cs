using System;
using System.Collections.Generic;
using System.IO;

namespace Kernel
{
    /// <summary>
    /// Something written against a day on the calendar - the van in for its
    /// MOT, a bank holiday, the day somebody is coming out with you.
    ///
    /// It is deliberately **about the day and not about the work**. A note on
    /// a job is a standing note about that house (`Job.Notes`) and a tag says
    /// what one visit was like; neither can say anything about a day with no
    /// work on it at all, which is most of what somebody wants to write down.
    /// So a note is kept against a date and nothing else, and a day with a
    /// note keeps it whether the work on that day is moved, done or cancelled.
    ///
    /// One global file (`daynotes.rjt`) like the rounds and the expense rules:
    /// notes are not a tax record, and last year's are worth as much as this
    /// week's to whoever is looking back at what happened.
    /// </summary>
    public class DayNote
    {
        /// <summary>the day it is written against. the date only, no time</summary>
        public DateTime Date { get; set; }

        public string Text { get; set; } = string.Empty;

        //  -----------------------------------------------------  the notes

        private static List<DayNote> _Notes = new List<DayNote>();

        private const string _FilePath = "daynotes.rjt";

        public struct SaveData
        {
            public List<DayNote> Notes;
        }

        /// <summary>every note there is, a copy of the list</summary>
        public static List<DayNote> Query()
        {
            return new List<DayNote>(_Notes);
        }

        /// <summary>what is written against a day, or nothing</summary>
        public static string TextFor(DateTime day)
        {
            DayNote note = Find(day);
            return note == null ? string.Empty : (note.Text ?? string.Empty);
        }

        /// <summary>is anything written against this day</summary>
        public static bool Has(DateTime day)
        {
            return TextFor(day).Length > 0;
        }

        private static DayNote Find(DateTime day)
        {
            DateTime wanted = day.Date;

            foreach (DayNote note in _Notes)
                if (note.Date.Date == wanted)
                    return note;

            return null;
        }

        /// <summary>
        /// Writes a note against a day.
        ///
        /// **Blank takes the note off.** There is no separate way of deleting
        /// one: rubbing out what is written is what somebody does when a note
        /// no longer applies, and a second button for it would only be a
        /// second thing to get wrong. An empty note left on the file would sit
        /// there marking a day that has nothing to say.
        ///
        /// Like the other setters in here it changes the note and does not
        /// write it down - <see cref="Save"/> is the caller's, the same way
        /// <see cref="Job.SetRound"/> leaves the saving to whoever set the
        /// round. It says whether anything actually changed, so a save is
        /// only made when there is something to save.
        /// </summary>
        /// <returns>true when something actually changed</returns>
        public static bool Set(DateTime day, string text)
        {
            text = (text ?? string.Empty).Trim();

            DayNote note = Find(day);

            if (text.Length == 0)
            {
                if (note == null)
                    return false;

                _Notes.Remove(note);
                return true;
            }

            if (note != null)
            {
                if (string.Equals(note.Text ?? string.Empty, text, StringComparison.CurrentCulture))
                    return false;

                note.Text = text;
            }
            else
                _Notes.Add(new DayNote() { Date = day.Date, Text = text });

            return true;
        }

        public static void DeleteData()
        {
            _Notes.Clear();
        }

        //  ------------------------------------------------------  the file

        private static string PathFor(string dir)
        {
            return Path.Combine(YearlyStore.Folder(dir), _FilePath);
        }

        public static void Save(string dir = null)
        {
            SaveData data = new SaveData();
            data.Notes = new List<DayNote>(_Notes);

            if (YearlyStore.WriteIfChanged(PathFor(dir), YearlyStore.Serialise(data)))
                DataStamp.Touch(DataStamp.DayNotes, dir);

            SyncNotifier.NotifySaved();
        }

        public static void Load(string dir = null)
        {
            _Notes.Clear();

            try
            {
                string path = PathFor(dir);
                if (!File.Exists(path))
                    return;

                SaveData data = YearlyStore.Deserialise<SaveData>(path);
                if (data.Notes == null)
                    return;

                //one note per day, and nothing blank: an older file - or two
                //devices' notes landing on the same day through the cloud -
                //must not leave a day with two things written on it
                foreach (DayNote note in data.Notes)
                {
                    if (note == null || string.IsNullOrWhiteSpace(note.Text))
                        continue;

                    note.Date = note.Date.Date;
                    note.Text = note.Text.Trim();

                    DayNote already = Find(note.Date);
                    if (already == null)
                        _Notes.Add(note);
                }
            }
            catch
            {
                //a file that will not read is left alone rather than written
                //over with nothing
            }
        }
    }
}
