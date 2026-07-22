using VREAndroids;
using RimWorld;
using Verse;

namespace VREAndroidsOverhaul
{
    // The subcore item. Empty when crafted in the polyanalyzer (used as an ingredient to build an
    // android), or carrying a stored persona once extracted from a (dead) android, ready to be
    // reprinted or resurrected into a new body.
    public class AndroidSubcore : ThingWithComps
    {
        public AndroidPersonaData personaData;

        public bool HasData => personaData != null && personaData.ContainsData;

        // Subcores carrying a stored persona must never stack (and must never absorb, or be absorbed
        // into, another subcore), or the unique identity data would be lost. Empty subcores still
        // stack freely as a crafting resource.
        public override bool CanStackWith(Thing other)
        {
            if (HasData || (other is AndroidSubcore otherSubcore && otherSubcore.HasData))
            {
                return false;
            }
            return base.CanStackWith(other);
        }

        public override string LabelNoCount =>
            HasData ? base.LabelNoCount + " (" + personaData.ColoredShortName + ")" : base.LabelNoCount;

        // Spell out which android this core belongs to right in the item's description (info card), the
        // way a cortical stack names its sleeve - otherwise stored cores are impossible to tell apart.
        public override string DescriptionFlavor =>
            HasData
                ? base.DescriptionFlavor + "\n\n" + "VREA.SubcoreStoredPersona".Translate(personaData.name.ToStringFull.Colorize(personaData.NameColor))
                : base.DescriptionFlavor;

        // A subcore carrying a persona shows that persona's name under the item on the map, instead of
        // the meaningless "1" stack count an empty crafting subcore would draw.
        public override void DrawGUIOverlay()
        {
            if (HasData)
            {
                if (Find.CameraDriver.CurrentZoom == CameraZoomRange.Closest)
                {
                    GenMapUI.DrawThingLabel(this, personaData.ShortName, personaData.NameColor);
                }
                return;
            }
            base.DrawGUIOverlay();
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (HasData)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                text += "VREA.SubcoreStoredPersona".Translate(personaData.name.ToStringFull.Colorize(personaData.NameColor));
            }
            return text;
        }

        // Destroying a subcore that still holds a persona is the android's permanent death. (Legitimate
        // consumption - reprinting from the subcore - hands the persona off first, so the item is empty
        // by the time it is destroyed and this does not fire.)
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (HasData)
            {
                AndroidDeath.RealDeathFromData(personaData);
            }
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref personaData, "personaData");
        }
    }
}
