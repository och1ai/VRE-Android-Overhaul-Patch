"""Static checks on the mod's data files. The C# half needs a RimWorld install to build, so it is not covered here."""
import collections
import pathlib
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parent.parent
XML_DIRS = ["About", "1.6/Defs", "1.6/Patches", "1.6/Languages"]
BASE_MOD = "vanillaracesexpanded.android"


def xml_files():
    files = [ROOT / "loadFolders.xml"]
    for directory in XML_DIRS:
        files.extend(sorted((ROOT / directory).rglob("*.xml")))
    return files


class WellFormed(unittest.TestCase):
    def test_every_xml_file_parses(self):
        for path in xml_files():
            with self.subTest(path=path.relative_to(ROOT)):
                ET.parse(path)


class About(unittest.TestCase):
    def setUp(self):
        self.about = ET.parse(ROOT / "About" / "About.xml").getroot()

    def test_identity(self):
        self.assertEqual(self.about.findtext("author"), "och1ai")
        package_id = self.about.findtext("packageId")
        self.assertRegex(package_id, r"^[a-z0-9]+(\.[a-z0-9]+)+$")
        self.assertIn("1.6", [li.text for li in self.about.find("supportedVersions")])
        self.assertRegex(self.about.findtext("modVersion"), r"^\d+\.\d+\.\d+$")

    def test_loads_after_and_requires_the_base_mod(self):
        required = [li.findtext("packageId") for li in self.about.find("modDependencies")]
        self.assertIn(BASE_MOD, required)
        self.assertIn(BASE_MOD, [li.text for li in self.about.find("loadAfter")])

    def test_description_has_no_filler_punctuation(self):
        description = self.about.findtext("description")
        self.assertNotIn(" - ", description.replace("Vanilla Races Expanded - ", ""))
        self.assertNotIn("—", description)

    def test_preview_image_exists(self):
        self.assertTrue((ROOT / "About" / "Preview.png").is_file())


class LoadFolders(unittest.TestCase):
    def test_listed_folders_exist(self):
        for folder in ET.parse(ROOT / "loadFolders.xml").getroot().iter("li"):
            self.assertTrue((ROOT / folder.text).is_dir(), folder.text)


class Defs(unittest.TestCase):
    def test_def_names_are_unique_per_def_type(self):
        seen = collections.defaultdict(list)
        for path in sorted((ROOT / "1.6" / "Defs").rglob("*.xml")):
            for definition in ET.parse(path).getroot():
                name = definition.findtext("defName")
                if name:
                    seen[(definition.tag, name)].append(path.name)
        duplicated = {key: files for key, files in seen.items() if len(files) > 1}
        self.assertEqual(duplicated, {})

    def test_own_textures_are_referenced_by_a_def(self):
        text = "".join(path.read_text(encoding="utf-8") for path in (ROOT / "1.6" / "Defs").rglob("*.xml"))
        textures = (ROOT / "1.6" / "Textures")
        for png in textures.rglob("*.png"):
            relative = png.relative_to(textures).with_suffix("").as_posix()
            with self.subTest(texture=relative):
                self.assertIn(relative, text)

    def test_textures_are_png_files(self):
        for png in (ROOT / "1.6" / "Textures").rglob("*.png"):
            with self.subTest(texture=png.name):
                self.assertEqual(png.read_bytes()[:8], b"\x89PNG\r\n\x1a\n")


class Patches(unittest.TestCase):
    def test_every_operation_names_an_xpath_or_nests_operations(self):
        for path in sorted((ROOT / "1.6" / "Patches").rglob("*.xml")):
            for operation in ET.parse(path).getroot().iter("Operation"):
                with self.subTest(patch=path.name, cls=operation.get("Class")):
                    has_xpath = operation.find("xpath") is not None
                    nests = operation.find("operations") is not None or operation.find("active") is not None
                    self.assertTrue(has_xpath or nests)
                    xpath = operation.findtext("xpath")
                    if xpath is not None:
                        self.assertTrue(xpath.strip())


class Language(unittest.TestCase):
    def keyed(self):
        keys = []
        for path in (ROOT / "1.6" / "Languages" / "English" / "Keyed").glob("*.xml"):
            keys.extend(child.tag for child in ET.parse(path).getroot())
        return keys

    def test_keyed_keys_are_unique(self):
        keys = self.keyed()
        self.assertEqual(len(keys), len(set(keys)))

    def test_translated_keys_used_in_code_exist(self):
        keys = set(self.keyed())
        used = set()
        for source in (ROOT / "1.6" / "Source").rglob("*.cs"):
            if "obj" in source.parts or "bin" in source.parts:
                continue
            used.update(re.findall(r'"(VREAOverhaul\.[A-Za-z0-9_]+)"\.Translate', source.read_text(encoding="utf-8")))
        self.assertEqual(sorted(used - keys), [])


if __name__ == "__main__":
    unittest.main()
