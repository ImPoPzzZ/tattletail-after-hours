namespace KaleidoscopeEndless
{
    /// <summary>One of Waygetter's own documents, found in the town and read on its own screen.</summary>
    public class RcNotice
    {
        public int Zone;
        public string Title, Text, Note;   // Note: a line after the text ("" for none)
    }

    /// <summary>The fifteen Recall Notices, three to a zone: the truth about the recall, a drip at a time. (From docs/RECALLED_LORE.md.)</summary>
    public static class RcNotices
    {
        public const int COUNT = 15;
        public static readonly RcNotice[] All =
        {
            // The suburbs
            new RcNotice { Zone = 0, Title = "RECALL NOTICE 001", Note = "",
                Text = "Dear Valued Family, your Tattletail has been selected for a routine check-up. Please leave your Tattletail on the porch. We will collect it. Please do not look for it afterwards." },
            new RcNotice { Zone = 0, Title = "LISTENING PROGRAM: WELCOME PACK", Note = "",
                Text = "Congratulations on joining the Listening Program! Your Tattletail will remember what it hears. (It will not remember that you were told.)" },
            new RcNotice { Zone = 0, Title = "NOTE, TAPED TO A FRIDGE", Note = "(a kid's handwriting)",
                Text = "Mom took the siren badge to work. She hasn't called. The truck came for the Hendersons first. It took all of them. It only counted heads over four feet." },
            // The mall
            new RcNotice { Zone = 1, Title = "FLOOR MODEL AGREEMENT", Note = "",
                Text = "Display units are not for sale. They are for watching. Do not disturb the Display Model. If it speaks, listen carefully and agree with it." },
            new RcNotice { Zone = 1, Title = "STORE MEMO", Note = "",
                Text = "The new Tattletail demo was a hit. Customers left their children at the Tattletail booth for hours. Foot traffic up 30%. We recommend leaving more children." },
            new RcNotice { Zone = 1, Title = "STAFF ONLY", Note = "",
                Text = "All employees must report to the loading dock for collection by 6pm. Management has already left." },
            // The school
            new RcNotice { Zone = 2, Title = "BEHAVIOR REPORT, GRADE 4", Note = "",
                Text = "Tattletail reports for this week: 41 lies, 12 broken rules, 3 secrets. Secrets have been forwarded. Thank you for cooperating." },
            new RcNotice { Zone = 2, Title = "HALL MONITOR: SPECIFICATION", Note = "",
                Text = "The Monitor does not hurt children. It records children. If a child is recorded breaking a rule, the child is \"flagged\". Flagged children are recalled with the toys." },
            new RcNotice { Zone = 2, Title = "TEACHER'S LETTER (UNSENT)", Note = "",
                Text = "I told them I had nothing to report. They said that's what the last teacher said. The roll call today was short." },
            // The junkyard
            new RcNotice { Zone = 3, Title = "DISPOSAL ORDER 77", Note = "",
                Text = "Recalled units that fail inspection are to be delivered to the Crusher. Units that fail the Crusher are to be delivered to Papa." },
            new RcNotice { Zone = 3, Title = "PAPA UNIT: MAINTENANCE LOG", Note = "",
                Text = "Papa has stopped clearing. He is keeping things. We do not know why. He keeps small shoes. Do not ask about the shoes." },
            new RcNotice { Zone = 3, Title = "WEIGHT TICKET", Note = "",
                Text = "Incoming: 4 trucks, adults. Outgoing: 0. The Line does not return. Do not log the Line." },
            // The Facility
            new RcNotice { Zone = 4, Title = "MAMA UNIT: OPERATING MANUAL, PAGE 1", Note = "",
                Text = "Mama is the home. Mama is the voice. Mama must never be allowed to go quiet. If the Tape stops, the toys will think for themselves. This is a fault." },
            new RcNotice { Zone = 4, Title = "THE MAMA FILE", Note = "",
                Text = "Seven towns. Seven Mamas. Seven Tapes. Town 1 is the pilot. If Town 1 fails, the Tape from Town 2 will be played over it." },
            new RcNotice { Zone = 4, Title = "A NOTE, FROM WHOEVER LEFT THE CASSETTE PLAYER ON", Note = "",
                Text = "If you're reading this, you got in. The Tape is in her back. Take it out. Don't play it in the dark." },
        };

        /// <summary>The notice's own number on the board of the three in its zone (0 to 2).</summary>
        public static int Slot(int index) { return index % 3; }

        /// <summary>The Facility's three unlock a line each on the destination board: the manual, the file, the note.</summary>
        public const int MANUAL = 12, FILE = 13, CASSETTE = 14;
    }
}
