using System.Collections.Generic;
using Verse;

namespace VREAndroidsOverhaul
{
    // The hardware a component depends on, and the components it cannot coexist with. The fork declares
    // both as fields on its own AndroidGeneDef subclass; an overlay cannot extend the original's def
    // class, so the same data rides along as a mod extension instead.
    //
    // Read through the ForkCompat helpers the ported editor windows call, which is where the requirement
    // gets both its tooltip line and its enforcement.
    public class AndroidComponentRequirements : DefModExtension
    {
        // The component can only be installed when at least one of these is on the android too - the way
        // Biotech's sanguophage genes require the hemogenic gene.
        public List<GeneDef> requiresOneOf;

        // Named by defName rather than by def so it can point at genes that do not exist on disk and are
        // generated at load time, such as the "incapable of social" aptitude.
        public List<string> conflictsWith;
    }
}
