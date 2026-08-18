using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml.Serialization;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;

using System.ComponentModel;

namespace Kernel
{
    //  The half of Job that exists for the screen.
    //
    //  A job is two things at once: a piece of the round - dates, money,
    //  what happened - and a row on a page, with colours, formatted strings,
    //  a tick box and a fold-out state. They used to live shoulder to
    //  shoulder in one file, which meant every change to how a row *looks*
    //  waded through the rules for what a job *is*, and the domain file
    //  needed the MAUI graphics types along for the ride.
    //
    //  So the display half lives here: everything whose only reason to
    //  exist is that a page binds to it or draws with it. Job.cs keeps the
    //  round - nothing in it should need a colour. Same class, same
    //  behaviour, two files with two jobs.
    //
    //  The rule for what goes where: if deleting a member could only ever
    //  break a screen, it belongs here; if it could break a figure, a file
    //  or a rule about the work, it belongs in Job.cs.
    public partial class Job
    {
        public GridLength Gr { get; set; } = new GridLength(0.3, GridUnitType.Star);

        [XmlIgnore]
        public bool DisableSwipe = false;

        /// <summary>whether the row can be swiped open at all. a booking
        /// summary row is not work and has nothing to swipe to</summary>
        public bool EnabledSwipe { get { return !DisableSwipe; } }

        /// <summary>
        /// The swipe on a list that also picks work out.
        ///
        /// While the ticks are on, swiping is not what the finger is there
        /// for - and the two cannot share a row: the swipe takes the touch as
        /// soon as the finger moves at all, which is what made a tick box
        /// need a drag before it would tick.
        ///
        /// It is a binding rather than something a page reaches into the list
        /// and sets, for the same reason SelectionModeEnabled is: the list is
        /// virtualised, so a row scrolled into view afterwards would never
        /// have been told, and half the rows ended up one way and half the
        /// other. Only the work list binds this - the calendar has no ticks,
        /// so its swipes are not the work list's business.
        /// </summary>
        public bool SwipeUnlessPicking { get { return EnabledSwipe && !SelectionMode; } }

        [XmlIgnore]
        public bool HaveTags
        {
            get { return Tags.Count > 0; }
        }

        /// <summary>the tags as one line, for the job rows and the history</summary>
        [XmlIgnore]
        public string TagsText
        {
            get { return string.Join(" • ", Tags); }
        }

        private void RefreshTags()
        {
            RaisePropertyChanged("Tags");
            RaisePropertyChanged("HaveTags");
            RaisePropertyChanged("TagsText");
        }

        /// <summary>true while there is a length worth putting on the row</summary>
        [XmlIgnore]
        public bool HaveLength
        {
            get { return Minutes > 0; }
        }

        /// <summary>the job's length as a tag, on the rows and the calendar</summary>
        [XmlIgnore]
        public string LengthText
        {
            get { return SpellMinutes(Minutes); }
        }

        /// <summary>minutes as somebody would say them</summary>
        public static string SpellMinutes(int minutes)
        {
            if (minutes <= 0)
                return string.Empty;

            if (minutes < 60)
                return $"{minutes} mins";

            int hours = minutes / 60;
            int rest = minutes % 60;

            string said = hours == 1 ? "1 hr" : $"{hours} hrs";

            return rest == 0 ? said : $"{said} {rest} mins";
        }

        /// <summary>
        /// this is where we can put temp data
        /// this is not saved
        /// </summary>
        [XmlIgnore]
        public object Data;

        /// <summary>
        /// The work list is picking jobs out rather than working through them.
        /// It is one switch for the whole round - either every row has a tick
        /// box or none of them do.
        ///
        /// It used to be set through a property on the job, which read a
        /// static behind the scenes but only told the one job it was set on
        /// that anything had changed. Rows built after that - the list is
        /// virtualised, so that is any row scrolled into view - read the
        /// static and drew a tick box while the rest of the list had none.
        /// That is where the boxes appearing on their own came from, and why
        /// they could not be got rid of: whichever rows were never told took
        /// no notice of being switched off either.
        ///
        /// So it is set through <see cref="SetSelectionMode"/>, which is the
        /// only thing that can change it and tells the whole round at once.
        /// </summary>
        public static bool SelectionMode { get; private set; }

        /// <summary>
        /// turns the tick boxes on or off for the whole list, clearing
        /// whatever was picked on the way out
        /// </summary>
        public static void SetSelectionMode(bool on)
        {
            SelectionMode = on;

            foreach (Job j in _Jobs)
            {
                if (!on)
                    j.IsSelected = false;

                j.RaisePropertyChanged("SelectionModeEnabled");
                j.RaisePropertyChanged("GridLengthCheckBoxWidth");

                //picking work turns the swipe off - see SwipeUnlessPicking
                j.RaisePropertyChanged("SwipeUnlessPicking");
            }
        }

        /// <summary>nothing picked, with the tick boxes left as they are</summary>
        public static void ClearSelection()
        {
            foreach (Job j in _Jobs)
                if (j.IsSelected)
                    j.IsSelected = false;
        }

        /// <summary>everything with its tick in</summary>
        public static List<Job> Selected()
        {
            return _Jobs.FindAll(x => x.IsSelected);
        }

        /// <summary>
        /// worked out rather than stored, so a row built at any point reads
        /// the same answer as every other row.
        ///
        /// the booking summary rows are not work and cannot be picked, so
        /// they never show a box whatever the list is doing
        /// </summary>
        [XmlIgnore]
        public bool SelectionModeEnabled
        {
            get { return SelectionMode && CustomerId != -1; }
        }

        [XmlIgnore]
        public double GridLengthCheckBoxWidth
        {
            get { return SelectionModeEnabled ? 0.3 : 0; }
        }


        [XmlIgnore]
        public Color AltColour { get; set; } = Colors.Transparent;
        [XmlIgnore]
        private bool _isSelected;
        [XmlIgnore]
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                RaisePropertyChanged("IsSelected");
            }
        }

        [XmlIgnore]
        private bool _collapsedInList;
        /// <summary>
        /// ui state: completed jobs show as a narrow faded row in day lists until tapped
        /// </summary>
        [XmlIgnore]
        public bool CollapsedInList
        {
            get { return _collapsedInList; }
            set
            {
                _collapsedInList = value;
                RaisePropertyChanged("CollapsedInList");
                RaisePropertyChanged("ExpandedInList");
            }
        }
        [XmlIgnore]
        public bool ExpandedInList { get { return !_collapsedInList; } }

        [XmlIgnore]
        public DateTime tmpDate;

        /// <summary>
        /// the one off tag in the lists. the section headings are jobs with
        /// no customer behind them, and a job that has been done is already
        /// saying so - the tag is there to warn that it will not come back
        /// </summary>
        [XmlIgnore]
        public bool ShowOneOff
        {
            get { return IsOneOff && CustomerId >= 0 && !IsCompleted && !HaveCanceled; }
        }

        private static string tmp;
        private static int tmpInt;
        public void Refresh()
        {

            //tmp = JobFormattedDueTime;
            //tmp = JobFormattedOwed;
            RaisePropertyChanged("JobFormattedOwed");
            RaisePropertyChanged("JobFormattedDueTime");
            RaisePropertyChanged("DueTextColour");
            RaisePropertyChanged("OwedTextColour");
            RaisePropertyChanged("ShowOwed");
            RaisePropertyChanged("PaymentPending");
            RaisePropertyChanged("PaymentPendingText");
            RaisePropertyChanged("IsMarked");
            RaisePropertyChanged("DoneActionText");
            RaisePropertyChanged("ShowPaidAction");
            RaisePropertyChanged("IsOneOff");
            RaisePropertyChanged("ShowOneOff");
            RaisePropertyChanged("CanDoAgain");
            RaisePropertyChanged("Round");
            RaisePropertyChanged("HaveRound");
            RaisePropertyChanged("RoundOrNone");
            RaisePropertyChanged("Minutes");
            RaisePropertyChanged("HaveLength");
            RaisePropertyChanged("LengthText");
            RefreshTags();
        }

        /// <summary>
        /// the job has already been marked done or paid, so the swipe offers
        /// Clear and More rather than Done and Done &amp; Paid again
        /// </summary>
        [XmlIgnore]
        public bool IsMarked
        {
            get { return IsCompleted || IsPaidFor; }
        }

        /// <summary>what the first swipe action does to this job as it stands</summary>
        [XmlIgnore]
        public string DoneActionText
        {
            get { return IsMarked ? "Clear" : "Done"; }
        }

        /// <summary>
        /// Done &amp; Paid is only worth offering on a job that is neither yet
        /// </summary>
        [XmlIgnore]
        public bool ShowPaidAction
        {
            get { return !IsMarked; }
        }

        public string JobFormattedStringPrice
        {
            get
            {
          //      RaisePropertyChanged("JobFormattedStringPrice");
                return $"Price {Gloable.CurrenceSymbol}{Price}";
            }
        }
        public string JobFormattedStringNotes
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedStringNotes");
                return $"{Notes}";
            }
        }
        public string JobFormattedString
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedString");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber} {Address.DisplayStreet} {Address.DisplayCity} {Address.DisplayArea}";

            }
        }

        public string JobFormattedStreet
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedStreet");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber} {Address.DisplayStreet}";

            }
        }


        public string JobFormattedHouseNumber
        {
            get
            {
           //     RaisePropertyChanged("JobFormattedHouseNumber");
                if (Address == null)
                    return string.Empty;
                return $"{Address.PropertyNameNumber}";

            }
        }
        public string JobFormattedStreetOnly
        {
            get
            {
              //  RaisePropertyChanged("JobFormattedStreetOnly");
                if (Address == null)
                    return string.Empty;
                return $"{Address.DisplayStreet}";

            }
        }

        public string JobFormattedCity
        {
            get
            {
            //    RaisePropertyChanged("JobFormattedCity");
                if (Address == null)
                    return string.Empty;
                return $"{Address.DisplayCity}";

            }
        }

        public string JobFormattedArea
        {
            get
            {
              //  RaisePropertyChanged("JobFormattedArea");
                if (Address == null)
                    return string.Empty;
                return $"{Address.DisplayArea}";

            }
        }

        public string JobFormattedSubString
        {
            get
            {
                RaisePropertyChanged("JobFormattedSubString");
                return $"Frequence {Frequence} Weekly {Gloable.CurrenceSymbol}{Price}";

            }
        }

        public string FormattedData
        {
            get
            {
             //   RaisePropertyChanged("FormattedData");
                if (IsCompleted)
                    return DateCompleated.ToShortDateString();
                else
                    return DueDate.ToShortDateString();
            }
        }

        public string JobFormattedDetails
        {
            get {
               // RaisePropertyChanged("JobFormattedDetails");
                if (IsCompleted)
                {
                    tmp = $"Completed on {DateCompleated.ToShortDateString()}.";
                    AlternativePrice chosen = ChosenAlternativePrice;
                    if (chosen == null)
                        tmp += $"Price {Gloable.CurrenceSymbol}{Price}";
                    else
                        tmp += $"Price {Gloable.CurrenceSymbol}{chosen.Price} for {chosen.Description}";
                }
                else
                    tmp = $"Job next due on {DueDate.ToShortDateString()}";

                return tmp;
            }

        }

        [XmlIgnore]
        public Color DueColorCode
        {
            get
            {
                return _dueColorCode;
            }
            set
            {
                _dueColorCode = value;
                RaisePropertyChanged("DueColorCode");
            }
        }
        private Color _dueColorCode = Colors.LightGray;
        [XmlIgnore]
        public Color DueColorTextCode
        {
            get
            {
                return _dueColorTextCode;
            }
            set
            {
                _dueColorTextCode = value;
                RaisePropertyChanged("DueColorTextCode");
            }
        }
        private Color _dueColorTextCode = Colors.LightGray;


        public string JobFormattedDueTime
        {
            get
            {
             //   RaisePropertyChanged("JobFormattedDueTime");
                if (IsCompleted)
                {
                    DueColorCode = Colors.LightGray;
                    DueColorTextCode = Colors.Black;
                    //counted from the day the work was actually done, not the day
                    //it fell due. this used to measure from DueDate, so a job
                    //cleaned months after it was due reported the whole overdue
                    //stretch as though that was how long ago it was cleaned -
                    //work finished this morning came up as "526 Days Ago"
                    int d = UsfulFuctions.Difference(DateCompleated, UsfulFuctions.DateNow);
                    switch (d)
                    {
                        case 0:
                            return $"Completed Today";

                        //Difference has no sign to it, so yesterday counts as
                        //one. the old -1 here could never be matched
                        case 1:
                            return $"Completed Yesterday";

                    }
                    return $"Completed {d} Days Ago";
                }

                DueColorTextCode = Colors.White;

                if (HaveCanceled)
                {
                    DueColorCode = Colors.Red;
                    return  "Canceled";
                }


                if (DueDate.DayOfYear == DateTime.Now.DayOfYear && DueDate.Year == DateTime.Now.Year) //if not due
                {
                    DueColorCode = Colors.Orange;
                    return "Due Today";

                }

                if (DueDate.Ticks > UsfulFuctions.DateNow.Ticks) //if not due
                {
                    DueColorCode = Colors.Blue;
                    tmpInt = UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow);
                    switch (tmpInt)
                    {
                        case 0:
                            return $"Due Today";

                        case 1:
                            return $"Due Tomorrow";

                        default:
                            return $"Due in {UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow)} Days";
                    }


                }

                DueColorCode = Colors.Red;
                return $"{UsfulFuctions.Difference(DueDate, UsfulFuctions.DateNow)} Days Late";

            }
        }

        [XmlIgnore]

        public Color OwedColorCode
        {
            get
            {
                return _owedColorCode;
            }
            set
            {
                _owedColorCode = value;
                RaisePropertyChanged("OwedColorCode");
            }
        }
        private Color _owedColorCode;

        /// <summary>
        /// when the work is due, as a colour to write the words in rather than
        /// a colour to sit them on.
        ///
        /// The chip colours above are picked to be read white on colour and
        /// are unreadable as text on a card - Yellow and LightBlue on white
        /// most of all - so the card rows on the work list have their own.
        /// The same states, said in colours that can be read on either theme,
        /// and the same ones Layouts/AllJobs uses so the two pages agree.
        /// </summary>
        [XmlIgnore]
        public Color DueTextColour
        {
            get
            {
                if (IsCompleted)
                    return Color.FromArgb("#6B7280");

                if (HaveCanceled)
                    return Color.FromArgb("#C62828");

                //today and late are what the round is worked off, so they are
                //the two that stand out; anything still to come is quiet
                if (DueDate.Date == UsfulFuctions.DateNow.Date)
                    return Color.FromArgb("#EF6C00");

                if (DueDate.Ticks > UsfulFuctions.DateNow.Ticks)
                    return Color.FromArgb("#6B7280");

                return Color.FromArgb("#C62828");
            }
        }

        /// <summary>
        /// what the customer owes, as a colour to write it in. See
        /// <see cref="DueTextColour"/> for why this is not OwedColorCode
        /// </summary>
        [XmlIgnore]
        public Color OwedTextColour
        {
            get
            {
                MatchCustomer();

                if (_customer == null)
                    return Colors.Transparent;

                if (_customer.Balance > 0)
                    return Color.FromArgb("#C62828");

                if (_customer.Balance < 0)
                    return Color.FromArgb("#2E7D32");

                return Color.FromArgb("#6B7280");
            }
        }

        public bool HaveJobNotes
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Notes);
            }
        }

        [XmlIgnore]
        public bool HaveJobName
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Name);
            }
        }

        public string JobFormattedOwedShort
        {
            get
            {
                MatchCustomer();
            //    RaisePropertyChanged("JobFormattedOwedShort");
                if (_customer == null)
                {
                    return string.Empty;
                }

                if (_customer.Balance >= 0)
                    return $"{_customer.Balance}";

                return "0";
            }
        }


        public void RefreshColors()
        {
            OwedColorCode = Colors.Yellow;

            MatchCustomer();

            if (_customer == null)
            {
                OwedColorCode = Colors.Transparent;
                return;
            }

            if (_customer.Balance == 0)
            {
                OwedColorCode = Colors.LightBlue;
                return;

            }

            if (_customer.Balance > 0)
            {
                OwedColorCode = Colors.Red;
                return;

            }

            OwedColorCode = Colors.Green;
            return;
        }

        /// <summary>
        /// hide the owed tag when there is no customer or nothing owed
        /// </summary>
        [XmlIgnore]
        public bool ShowOwed
        {
            get
            {
                MatchCustomer();
                return _customer != null && _customer.Balance != 0;
            }
        }

        public string JobFormattedOwed
        {
            get
            {
                MatchCustomer();


      //         RaisePropertyChanged("JobFormattedOwed");

                if (_customer == null)
                {

                    return string.Empty;
                }

                if (_customer.Balance == 0)
                {
                    return  "Nothing Owed";

                }

                if (_customer.Balance > 0)
                {
                    return $"Owes {Gloable.CurrenceSymbol}{_customer.Balance}";

                }

                return $"{Gloable.CurrenceSymbol}{Math.Abs(_customer.Balance)} In Credit";

            }
        }
    }
}
