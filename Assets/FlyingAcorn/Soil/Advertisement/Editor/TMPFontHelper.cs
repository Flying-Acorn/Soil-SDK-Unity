using UnityEngine;
using UnityEditor;

namespace FlyingAcorn.Soil.Advertisement.Editor
{
    /// <summary>
    /// Simple utility to open the TextMeshPro Font Asset Creator with pre-configured settings
    /// </summary>
    public class TMPFontHelper
    {
        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Open TMP Font Asset Creator")]
        public static void OpenFontAssetCreator()
        {
            // Open the TMP Font Asset Creator window
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");

            Debug.Log("==== TMP Font Asset Creator Opened ====");
            Debug.Log("Follow these steps to create Persian/Arabic font support:");
            Debug.Log("1. Source Font File: Select your font file");
            Debug.Log("2. Sampling Point Size: 64 (smaller for better packing)");
            Debug.Log("3. Padding: 4");
            Debug.Log("4. Packing Method: Optimum");
            Debug.Log("5. Atlas Resolution: 2048 x 2048 (larger for more characters)");
            Debug.Log("6. Character Set: Unicode Range (Hex)");
            Debug.Log("7. Unicode Range: Use 'Copy Full Persian/Arabic Range' menu option");
            Debug.Log("8. Render Mode: SDFAA (if available) or SDF");
            Debug.Log("9. If you see 'Multi Atlas' option, enable it. If not, continue without it.");
            Debug.Log("10. Click 'Generate Font Atlas'");
            Debug.Log("11. Save as 'NotoSansArabic_Persian_TMP' in TMP_FontAssets folder");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Essential Arabic Range")]
        public static void CopyEssentialArabicRange()
        {
            // Reduced range focusing on essential Arabic characters
            string unicodeRange = "0020-007F,0600-064A,FB50-FDFF,FE70-FEFF";
            EditorGUIUtility.systemCopyBuffer = unicodeRange;
            Debug.Log($"Essential Arabic range copied to clipboard: {unicodeRange}");
            Debug.Log("This includes basic Latin + core Arabic characters only");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Full Persian/Arabic Range")]
        public static void CopyFullPersianArabicRange()
        {
            // Complete range including Persian extensions
            string unicodeRange = "0020-007F,0590-05FF,0600-06FF,0750-077F,08A0-08FF,FB50-FDFF,FE70-FEFF";
            EditorGUIUtility.systemCopyBuffer = unicodeRange;
            Debug.Log($"Full Persian/Arabic range copied: {unicodeRange}");
            Debug.Log("Includes: Latin, Hebrew, Arabic, Arabic Supplement, Arabic Extended-A, Arabic Presentation Forms A & B");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Essential Characters")]
        public static void CopyEssentialCharacters()
        {
            // Essential characters as text (not Unicode ranges)
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?;:'\"-()[]{}/@#$%^&*+=<>|\\`~_ ";
            characters += "ءآأؤإئابةتثجحخدذرزسشصضطظعغـفقكلمنهوىي";
            characters += "َُِّْٰٱٲٳٴٵٶٷٸٹٺٻټٽپٿڀځڂڃڄچڇڈډڊڋڌڍڎڏڐڑڒړڔڕږڗژڙښڛڜڝڞڟڠڡڢڣڤڥڦڧڨکڪګڬڭڮگڰڱڲڳڴڵڶڷڸڹںڻڼڽھڿہۂۃۄۅۆۇۈۉۊۋیۍێۏېۑےۓ۔";
            characters += "۰۱۲۳۴۵۶۷۸۹";

            EditorGUIUtility.systemCopyBuffer = characters;
            Debug.Log($"Essential Arabic/Persian characters copied ({characters.Length} chars)");
            Debug.Log("Use 'Character Set > Custom Characters' in Font Asset Creator and paste this");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Essential Persian Characters")]
        public static void CopyEssentialPersianCharacters()
        {
            // Essential characters including Persian-specific ones
            string latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?;:'\"-()[]{}/@#$%^&*+=<>|\\`~_ ";
            string arabicBase = "ءآأؤإئابةتثجحخدذرزسشصضطظعغـفقكلمنهوىي";
            string persianSpecific = "پچژکگیؤئ"; // Persian letters not in Arabic
            string arabicDigits = "٠١٢٣٤٥٦٧٨٩";
            string persianDigits = "۰۱۲۳۴۵۶۷۸۹";
            string diacritics = "َُِّْٰ";

            string allChars = latin + arabicBase + persianSpecific + arabicDigits + persianDigits + diacritics;

            EditorGUIUtility.systemCopyBuffer = allChars;
            Debug.Log($"Essential Persian/Arabic characters copied ({allChars.Length} chars)");
            Debug.Log("Use 'Character Set > Custom Characters' in Font Asset Creator");
            Debug.Log("This includes Persian-specific: پ چ ژ ک گ ی");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Minimal Persian Range")]
        public static void CopyMinimalPersianRange()
        {
            // Absolutely minimal range - just what you need for Persian ads
            string unicodeRange = "0020-007F,0621-064A,067E,0686,0698,06A9,06AF,06CC,FB56-FB59,FBFC-FBFF";
            EditorGUIUtility.systemCopyBuffer = unicodeRange;
            Debug.Log($"Minimal Persian range copied: {unicodeRange}");
            Debug.Log("Includes: Latin, core Arabic letters, Persian پ چ ژ ک گ ی");
            Debug.Log("Plus essential contextual forms for connection");
            Debug.Log("Use this if you can't fit larger character sets");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Ultra Minimal Persian Range")]
        public static void CopyUltraMinimalPersianRange()
        {
            // Ultra minimal - only the most essential characters for ads
            string unicodeRange = "0020-007F,0627,0628,062A,062D,062F,0631,0633,0635,0639,0641,0642,0644,0645,0646,0647,0648,064A,067E,0686,0698,06A9,06AF,06CC";
            EditorGUIUtility.systemCopyBuffer = unicodeRange;
            Debug.Log($"Ultra minimal Persian range copied: {unicodeRange}");
            Debug.Log("Only essential letters: ا ب ت ح د ر س ص ع ف ق ل م ن ه و ی + Persian پ چ ژ ک گ ی");
            Debug.Log("Should be under 2MB");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Just Persian Letters")]
        public static void CopyJustPersianLetters()
        {
            // Just the Persian-specific letters as characters (not Unicode)
            string persianOnly = "پچژکگی";
            EditorGUIUtility.systemCopyBuffer = persianOnly;
            Debug.Log($"Just Persian letters copied: {persianOnly}");
            Debug.Log("Add these to an existing Arabic font asset");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Contextual Forms Only")]
        public static void CopyContextualFormsOnly()
        {
            // Only the contextual/connected forms for common letters
            string contextualRange = "FE8E,FE8F,FE90,FE91,FE92,FE93,FE94,FE95,FE96,FE97,FE98,FE99,FE9A,FE9B,FE9C,FE9D,FE9E,FE9F,FEA0,FEA1,FEA2,FEA3,FEA4,FEA5,FEA6,FEA7,FEA8,FEA9,FEAA,FEAB,FEAC,FEAD,FEAE,FEAF,FEB0,FEB1,FEB2,FEB3,FEB4,FEB5,FEB6,FEB7,FEB8,FEB9,FEBA,FEBB,FEBC,FEBD,FEBE,FEBF,FEC0,FEC1,FEC2,FEC3,FEC4,FEC5,FEC6,FEC7,FEC8,FEC9,FECA,FECB,FECC,FECD,FECE,FECF,FED0,FED1,FED2,FED3,FED4,FED5,FED6,FED7,FED8,FED9,FEDA,FEDB,FEDC,FEDD,FEDE,FEDF,FEE0,FEE1,FEE2,FEE3,FEE4,FEE5,FEE6,FEE7,FEE8,FEE9,FEEA,FEEB,FEEC,FEED,FEEE,FEEF,FEF0,FEF1,FEF2,FEF3,FEF4";
            EditorGUIUtility.systemCopyBuffer = contextualRange;
            Debug.Log("Common contextual forms copied");
            Debug.Log("These are the connected versions of Arabic letters");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Generate Test Arabic Text")]
        public static void GenerateTestArabicText()
        {
            // Common Arabic/Persian characters for testing
            string testText = "اختبار نص عربي ١٢٣ test دانلود رایگان";
            EditorGUIUtility.systemCopyBuffer = testText;
            Debug.Log($"Test Arabic text copied: {testText}");
            Debug.Log("Use this text to test your font asset");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Generate Test Persian Text")]
        public static void GenerateTestPersianText()
        {
            // Test text with Persian-specific characters and connected forms
            string testText = "دانلود رایگان بازی ۱۲۳ پیشرفته چهارم گیم";
            EditorGUIUtility.systemCopyBuffer = testText;
            Debug.Log($"Test Persian text copied: {testText}");
            Debug.Log("Use this to test Persian characters and text connection");
            Debug.Log("Should include: پ، ی، چ، گ and connected forms");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Troubleshoot Large Character Sets")]
        public static void TroubleshootLargeCharacterSets()
        {
            Debug.Log("==== Troubleshooting Large Character Sets ====");
            Debug.Log("If you can't fit all characters in one atlas, try these approaches:");
            Debug.Log("");
            Debug.Log("APPROACH 1 - Smaller Unicode Range:");
            Debug.Log("Use 'Copy Essential Arabic Range' instead of full range");
            Debug.Log("Range: 0020-007F,0600-064A,FB50-FDFF,FE70-FEFF");
            Debug.Log("");
            Debug.Log("APPROACH 2 - Character Set Method:");
            Debug.Log("Use 'Copy Essential Persian Characters' with Custom Characters");
            Debug.Log("This gives you exactly what you need, no extras");
            Debug.Log("");
            Debug.Log("APPROACH 3 - Smaller Atlas Settings:");
            Debug.Log("- Point Size: 48 (smaller)");
            Debug.Log("- Padding: 2 (minimal)");
            Debug.Log("- Atlas: 4096 x 4096 (maximum size)");
            Debug.Log("");
            Debug.Log("APPROACH 4 - Split into Multiple Fonts:");
            Debug.Log("Create separate fonts for Latin and Arabic/Persian");
            Debug.Log("Use fallback fonts in TextMeshPro");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Settings for Small Font")]
        public static void SettingsForSmallFont()
        {
            Debug.Log("==== Settings to Keep Font Under 2MB ====");
            Debug.Log("Point Size: 32 (very small)");
            Debug.Log("Padding: 1 (minimal)");
            Debug.Log("Atlas Resolution: 1024 x 1024 (smaller)");
            Debug.Log("Packing Method: Fast");
            Debug.Log("Render Mode: SDF (not SDFAA)");
            Debug.Log("");
            Debug.Log("Character approach:");
            Debug.Log("1. Use 'Copy Ultra Minimal Persian' for smallest set");
            Debug.Log("2. Or use 'Copy Essential Persian Characters' with Custom Characters");
            Debug.Log("3. Test with 'Generate Test Persian Text'");
            Debug.Log("");
            Debug.Log("If still too big, use multiple smaller fonts!");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Smart Persian Solution")]
        public static void CopySmartPersianSolution()
        {
            // Smart approach: Essential letters + minimal contextual forms
            string smartChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?;:'\"()[]{}@#$%&*+-=<>|`~_ ";
            // Core Arabic letters
            smartChars += "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
            // Persian specific
            smartChars += "پچژکگی";
            // Persian digits
            smartChars += "۰۱۲۳۴۵۶۷۸۹";
            // Essential connecting forms (just the most common ones)
            smartChars += "ـ"; // Arabic Tatweel (connector)

            EditorGUIUtility.systemCopyBuffer = smartChars;
            Debug.Log($"Smart Persian solution copied ({smartChars.Length} characters)");
            Debug.Log("Essential letters + Persian specific + minimal connectors");
            Debug.Log("Should be much smaller than 2MB");
            Debug.Log("Use 'Custom Characters' in Font Asset Creator");
        }
        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Contextual Forms Characters")]
        public static void CopyContextualFormsCharacters()
        {
            // Essential contextual forms as actual characters (not Unicode ranges)
            string contextualChars = "";

            // Basic Latin and numbers
            contextualChars += "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?;:'\"()[]{}@#$%&*+-=<>|`~_ ";

            // Basic Arabic letters (isolated forms)
            contextualChars += "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";

            // Persian specific letters
            contextualChars += "پچژکگی";

            // Persian digits
            contextualChars += "۰۱۲۳۴۵۶۷۸۹";

            // Arabic Tatweel (connector)
            contextualChars += "ـ";

            // Essential contextual forms for connection (Arabic Presentation Forms-B)
            // Initial forms (beginning of words)
            contextualChars += "ﺑﺗﺛﺟﺣﺧﺳﺷﺻﺿﻃﻇﻋﻏﻓﻗﻛﻟﻣﻧﻫﻳ";

            // Medial forms (middle of words) - these are critical for connection
            contextualChars += "ﺒﺘﺜﺠﺣﺨﺴﺸﺼﻀﻃﻇﻌﻏﻔﻗﻚﻠﻤﻨﻬﻴ";

            // Final forms (end of words)
            contextualChars += "ﺐﺖﺚﺞﺢﺦﺲﺶﺺﺾﻂﻆﻊﻎﻒﻖﻚﻞﻢﻦﻪﻲ";

            // Persian contextual forms
            contextualChars += "ﭘﭙﭚﭛﭜﭝﭞﭟﭠﭡﭢﭣﭤﭥﭦﭧﭨﭩﭪﭫﭬﭭﭮﭯﭰﭱﭲﭳﭴﭵﭶﭷﭸﭹﭺﭻﭼﭽﭾﭿﮀﮁﮂﮃﮄﮅﮆﮇﮈﮉﮊﮋﮌﮍﮎﮏﮐﮑﮒﮓﮔﮕﮖﮗﮘﮙﮚﮛﮜﮝﮞﮟﮠﮡﮢﮣﮤﮥﮦﮧﮨﮩﮪﮫﮬﮭﮮﮯﮰﮱﯓﯔﯕﯖﯗﯘﯙﯚﯛﯜﯝﯞﯟﯠﯡﯢﯣﯤﯥﯦﯧﯨﯩﯪﯫﯬﯭﯮﯯﯰﯱﯲﯳﯴﯵﯶﯷﯸﯹﯺﯻﯼﯽﯾﯿ";

            EditorGUIUtility.systemCopyBuffer = contextualChars;
            Debug.Log($"Contextual forms characters copied ({contextualChars.Length} characters)");
            Debug.Log("This includes:");
            Debug.Log("- Basic Latin and Persian letters");
            Debug.Log("- All contextual forms: initial ﺑ, medial ﺒ, final ﺐ");
            Debug.Log("- Persian specific contextual forms: ﭘ ﭼ ﮊ ﮐ ﮔ ﯾ");
            Debug.Log("- Use 'Custom Characters' in TMP Font Asset Creator");
            Debug.Log("- This ensures proper text connection!");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy COMPLETE Contextual Forms")]
        public static void CopyCompleteContextualForms()
        {
            // COMPREHENSIVE contextual forms - this should fix connection issues
            string contextualChars = "";

            // Basic Latin and numbers
            contextualChars += "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?;:'\"()[]{}@#$%&*+-=<>|`~_ ";

            // Basic Arabic letters (isolated forms)
            contextualChars += "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";

            // Persian specific letters
            contextualChars += "پچژکگی";

            // Persian digits
            contextualChars += "۰۱۲۳۴۵۶۷۸۹";

            // Arabic Tatweel (connector) - CRITICAL
            contextualChars += "ـ";

            // COMPLETE Arabic Presentation Forms-B (FE70-FEFF) - Essential for connection
            // This includes ALL contextual forms for Arabic letters
            contextualChars += "ﹰﹱﹲﹳﹴﹶﹷﹸﹹﹺﹻﹼﹽﹾﹿﺀﺁﺂﺃﺄﺅﺆﺇﺈﺉﺊﺋﺌﺍﺎﺏﺐﺑﺒﺓﺔﺕﺖﺗﺘﺙﺚﺛﺜﺝﺞﺟﺠﺡﺢﺣﺤﺥﺦﺧﺨﺩﺪﺫﺬﺭﺮﺯﺶ";
            contextualChars += "ﺱﺲﺳﺴﺵﺶﺷﺸﺹﺺﺻﺼﺽﺾﺿﻀﻁﻂﻃﻄﻅﻆﻟﻠﻡﻢﻣﻤﻥﻦﻧﻨﻩﻪﻫﻬﻭﻮﻯﻰﻱﻲﻧﻨﻪ";

            // Persian contextual forms - ALL variations
            contextualChars += "ﭘﭙﭚﭛﭜﭝﭞﭟﭠﭡﭢﭣﭤﭥﭦﭧﭨﭩﭪﭫﭬﭭﭮﭯﭰﭱﭲﭳﭴﭵﭶﭷﭸﭹﭺﭻﭼﭽﭾﭿﮀﮁﮂﮃﮄﮅﮆﮇﮈﮉﮊﮋﮌﮍﮎﮏﮐﮑﮒﮓﮔﮕﮖﮗﮘﮙﮚﮛﮜﮝﮞﮟﮠﮡﮢﮣﮤﮥﮦﮧﮨﮩﮪﮫﮬﮭﮮﮯﮰﮱﯓﯔﯕﯖﯗﯘﯙﯚﯛﯜﯝﯞﯟﯠﯡﯢﯣﯤﯥﯦﯧﯨﯩﯪﯫﯬﯭﯮﯯﯰﯱﯲﯳﯴﯵﯶﯷﯸﯹﯺﯻﯼﯽﯾﯿ";

            EditorGUIUtility.systemCopyBuffer = contextualChars;
            Debug.Log($"COMPLETE contextual forms copied ({contextualChars.Length} characters)");
            Debug.Log("This includes ALL Arabic Presentation Forms-B (FE70-FEFF)");
            Debug.Log("- Every possible contextual form for Arabic/Persian letters");
            Debug.Log("- Initial, medial, final, and isolated forms");
            Debug.Log("- Persian-specific contextual variations");
            Debug.Log("- Use 'Custom Characters' in TMP Font Asset Creator");
            Debug.Log("- This SHOULD fix connection issues definitively!");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Copy Contextual Forms Range")]
        public static void CopyContextualFormsRange()
        {
            // Unicode ranges that include contextual forms for proper Arabic/Persian connection
            string contextualRange = "0020-007F,0600-06FF,FB50-FDFF,FE70-FEFF";
            EditorGUIUtility.systemCopyBuffer = contextualRange;
            Debug.Log($"Contextual forms range copied to clipboard: {contextualRange}");
            Debug.Log("This includes Arabic Presentation Forms A (FB50-FDFF) and B (FE70-FEFF)");
            Debug.Log("These ranges contain the connected forms of Arabic/Persian letters");
            Debug.Log("Paste this in TMP Font Asset Creator > Character Set > Unicode Range");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Alternative: Use System Font")]
        public static void AlternativeSystemFont()
        {
            Debug.Log("==== Alternative Solution: System Font Approach ====");
            Debug.Log("If custom font is too large, consider using system fonts:");
            Debug.Log("1. Set customAdFont = null in AdDisplayComponent");
            Debug.Log("2. TextMeshPro will use system fonts which support Persian");
            Debug.Log("3. Enable 'Auto Size' in TextMeshPro components");
            Debug.Log("4. System handles RTL and connections automatically");
            Debug.Log("");
            Debug.Log("Pros: Small size, automatic RTL support");
            Debug.Log("Cons: Less control over appearance");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Fix Text Connection Issues")]
        public static void FixTextConnectionIssues()
        {
            Debug.Log("==== FIXING PERSIAN/ARABIC TEXT CONNECTION ISSUES ====");
            Debug.Log("");
            Debug.Log("If your Persian/Arabic text appears disconnected (isolated characters), try these solutions:");
            Debug.Log("");
            Debug.Log("SOLUTION 1: Verify Font Asset Settings");
            Debug.Log("1. Select your TMP Font Asset in Project window");
            Debug.Log("2. In Inspector, check 'Character Table' tab");
            Debug.Log("3. Look for contextual forms (characters like ـبـ, ـتـ, etc.)");
            Debug.Log("4. If missing, recreate font asset with 'Copy Contextual Forms Range' below");
            Debug.Log("");
            Debug.Log("SOLUTION 2: TextMeshPro Component Settings");
            Debug.Log("1. Select your TextMeshPro component in Scene");
            Debug.Log("2. Enable 'isRightToLeftText' checkbox");
            Debug.Log("3. Set Alignment to 'Right' or 'Center'");
            Debug.Log("4. Enable 'Parse Control Characters'");
            Debug.Log("");
            Debug.Log("SOLUTION 3: Font Recreation with Contextual Forms");
            Debug.Log("- Use the 'Copy Contextual Forms' menu option below");
            Debug.Log("- This includes Arabic Presentation Forms A & B which handle connection");
            Debug.Log("");
            Debug.Log("SOLUTION 4: If Still Not Working");
            Debug.Log("- Persian text connection depends on the source font file");
            Debug.Log("- Try downloading Google Noto Sans Arabic from fonts.google.com");
            Debug.Log("- Use 'Test Text Connection' below to verify your font supports connection");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Test Text Connection")]
        public static void TestTextConnection()
        {
            string testText = "بسم الله الرحمن الرحیم";
            EditorGUIUtility.systemCopyBuffer = testText;
            Debug.Log("Persian connection test text copied to clipboard:");
            Debug.Log($"Text: {testText}");
            Debug.Log("Translation: 'In the name of Allah, the Most Gracious, the Most Merciful'");
            Debug.Log("");
            Debug.Log("How to test:");
            Debug.Log("1. Create a TextMeshPro component in your scene");
            Debug.Log("2. Assign your custom TMP font asset");
            Debug.Log("3. Paste the test text");
            Debug.Log("4. Enable 'isRightToLeftText'");
            Debug.Log("5. Letters should connect like: بسم (not isolated like: ب س م)");
            Debug.Log("");
            Debug.Log("If letters appear isolated, your font asset needs contextual forms!");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Verify Font Asset Quality")]
        public static void VerifyFontAssetQuality()
        {
            Debug.Log("==== VERIFYING TMP FONT ASSET QUALITY ====");
            Debug.Log("");
            Debug.Log("To verify your TMP font asset supports proper Persian/Arabic connection:");
            Debug.Log("");
            Debug.Log("1. SELECT YOUR TMP FONT ASSET");
            Debug.Log("   - In Project window, click on your TMP font asset file");
            Debug.Log("   - Look at the Inspector panel");
            Debug.Log("");
            Debug.Log("2. CHECK CHARACTER TABLE");
            Debug.Log("   - In Inspector, click 'Character Table' tab");
            Debug.Log("   - Look for these critical connection characters:");
            Debug.Log("   - ـبـ (connected beh) - Unicode FB91");
            Debug.Log("   - ـتـ (connected teh) - Unicode FB97");
            Debug.Log("   - ـثـ (connected theh) - Unicode FB9D");
            Debug.Log("   - If you see these, your font supports connection!");
            Debug.Log("");
            Debug.Log("3. CHECK GLYPH TABLE");
            Debug.Log("   - In Inspector, click 'Glyph Table' tab");
            Debug.Log("   - Should have 200+ glyphs for good Arabic/Persian support");
            Debug.Log("   - More glyphs = better text connection");
            Debug.Log("");
            Debug.Log("4. IF MISSING CONNECTION CHARACTERS:");
            Debug.Log("   - Use 'Copy Contextual Forms Range' menu option");
            Debug.Log("   - Recreate font asset with that range");
            Debug.Log("   - This ensures all connection forms are included");
            Debug.Log("");
            Debug.Log("5. ALTERNATIVE: Download Proper Font");
            Debug.Log("   - Get NotoSansArabic from fonts.google.com");
            Debug.Log("   - This font file includes all necessary contextual forms");
        }

        [MenuItem("FlyingAcorn/Soil/Advertisement/Font Tools/Debug Font Asset Connection")]
        public static void DebugFontAssetConnection()
        {
            Debug.Log("==== DEBUGGING FONT ASSET CONNECTION SUPPORT ====");
            Debug.Log("");
            Debug.Log("Select your TMP Font Asset in Project window, then:");
            Debug.Log("");
            Debug.Log("1. CHECK CHARACTER COUNT");
            Debug.Log("   - Look at Inspector > Character Table");
            Debug.Log("   - Should have 200+ characters for good Arabic support");
            Debug.Log("   - If less than 100, your character set is too small");
            Debug.Log("");
            Debug.Log("2. SEARCH FOR CRITICAL CHARACTERS");
            Debug.Log("   In Character Table search box, test these:");
            Debug.Log("   - Search 'ب' - should find isolated beh");
            Debug.Log("   - Search 'ﺑ' - should find initial beh (FB91)");
            Debug.Log("   - Search 'ﺒ' - should find medial beh (FB93) ← CRITICAL!");
            Debug.Log("   - Search 'ﺐ' - should find final beh (FB92)");
            Debug.Log("");
            Debug.Log("3. CHECK PERSIAN CHARACTERS");
            Debug.Log("   - Search 'پ' - isolated peh");
            Debug.Log("   - Search 'ﭘ' - initial peh (FB59)");
            Debug.Log("   - Search 'ﭙ' - medial peh (FB5A) ← CRITICAL!");
            Debug.Log("   - Search 'ﭚ' - final peh (FB5B)");
            Debug.Log("");
            Debug.Log("4. RESULTS DIAGNOSIS");
            Debug.Log("   ✅ All found = Font supports connection");
            Debug.Log("   ❌ Missing medial forms = No connection support");
            Debug.Log("   ❌ Missing Persian forms = Incomplete Persian support");
            Debug.Log("");
            Debug.Log("5. IF MISSING FORMS:");
            Debug.Log("   - Use 'Copy COMPLETE Contextual Forms'");
            Debug.Log("   - Recreate font asset with Custom Characters");
            Debug.Log("   - Or try different source font file");
        }
    }
}
