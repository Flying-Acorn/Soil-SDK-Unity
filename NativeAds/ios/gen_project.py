#!/usr/bin/env python3
"""Writes SoilAdsHarness.xcodeproj: a host app (the plugin sources + Unity stubs) and a hosted
XCTest bundle. The plugin files are referenced in place from Assets/, nothing is copied.
Re-run after adding or removing files:  python3 NativeAds/ios/gen_project.py"""
import hashlib
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
PLUGIN_REL = "../../Assets/FlyingAcorn/Soil/Advertisement/Plugins/iOS/SoilAds"
PLUGIN_DIR = os.path.normpath(os.path.join(HERE, PLUGIN_REL))
PROJECT = os.path.join(HERE, "SoilAdsHarness.xcodeproj")
APP = "SoilAdsHost"
TESTS = "SoilAdsTests"


def uid(*parts):
    return hashlib.md5("/".join(parts).encode()).hexdigest()[:24].upper()


def listdir(path, exts):
    return sorted(f for f in os.listdir(path) if os.path.splitext(f)[1] in exts)


plugin_files = listdir(PLUGIN_DIR, {".h", ".m"})
host_files = listdir(os.path.join(HERE, "HostApp"), {".h", ".m"})
test_files = listdir(os.path.join(HERE, "Tests"), {".h", ".m"})
resource_files = sorted(f for f in os.listdir(os.path.join(HERE, "Tests/Resources")) if not f.startswith("."))

objects = {}


def add(key, body):
    objects[key] = body


def file_type(name):
    return {
        ".h": "sourcecode.c.h", ".m": "sourcecode.c.objc", ".mp4": "file", ".m4a": "file",
        ".png": "image.png", ".jpg": "image.jpeg",
    }.get(os.path.splitext(name)[1], "file")


groups = {
    "SoilAds": (PLUGIN_REL, plugin_files),
    "HostApp": ("HostApp", host_files),
    "Tests": ("Tests", test_files),
    "Resources": ("Tests/Resources", resource_files),
}
file_refs = {}
for group, (path, files) in groups.items():
    for name in files:
        ref = uid("ref", group, name)
        file_refs[(group, name)] = ref
        add(ref, f'{{isa = PBXFileReference; lastKnownFileType = {file_type(name)}; path = "{name}"; sourceTree = "<group>"; }}')
    add(uid("group", group), "{isa = PBXGroup; children = (%s); path = \"%s\"; sourceTree = SOURCE_ROOT; }"
        % (" ".join(file_refs[(group, n)] + "," for n in files), path))

app_product = uid("product", APP)
test_product = uid("product", TESTS)
add(app_product, f'{{isa = PBXFileReference; explicitFileType = wrapper.application; includeInIndex = 0; path = "{APP}.app"; sourceTree = BUILT_PRODUCTS_DIR; }}')
add(test_product, f'{{isa = PBXFileReference; explicitFileType = wrapper.cfbundle; includeInIndex = 0; path = "{TESTS}.xctest"; sourceTree = BUILT_PRODUCTS_DIR; }}')
products_group = uid("group", "Products")
add(products_group, f"{{isa = PBXGroup; children = ({app_product}, {test_product}, ); name = Products; sourceTree = \"<group>\"; }}")
main_group = uid("group", "main")
add(main_group, "{isa = PBXGroup; children = (%s %s); sourceTree = \"<group>\"; }"
    % (" ".join(uid("group", g) + "," for g in groups), products_group + ","))


def unity_compile_flags(name):
    """The per-file iOS compiler flags Unity puts in its Xcode project: CompileFlags in the .meta."""
    meta = os.path.join(HERE, PLUGIN_REL, name + ".meta")
    if not os.path.exists(meta):
        return ""
    # Only the iOS entry: it ends where the next platform's "- first:" starts.
    entry = re.search(r"iPhone: iOS\n(.*?)(?:\n  - first:|\Z)", open(meta).read(), re.S)
    match = entry and re.search(r"CompileFlags:[ \t]*([^\n]*)", entry.group(1))
    return match.group(1).strip() if match else ""


def build_files(phase, group, names):
    keys = []
    for name in names:
        key = uid("build", phase, group, name)
        flags = unity_compile_flags(name) if group == "SoilAds" else ""
        extra = f' settings = {{COMPILER_FLAGS = "{flags}"; }};' if flags else ""
        add(key, f"{{isa = PBXBuildFile; fileRef = {file_refs[(group, name)]};{extra} }}")
        keys.append(key)
    return keys


app_sources = build_files("appsrc", "SoilAds", [f for f in plugin_files if f.endswith(".m")]) + \
    build_files("appsrc", "HostApp", [f for f in host_files if f.endswith(".m")])
test_sources = build_files("testsrc", "Tests", [f for f in test_files if f.endswith(".m")])
test_resources = build_files("testres", "Resources", resource_files)


def phase(key, isa, files):
    add(key, "{isa = %s; buildActionMask = 2147483647; files = (%s); runOnlyForDeploymentPostprocessing = 0; }"
        % (isa, " ".join(f + "," for f in files)))
    return key


app_phases = [phase(uid("phase", APP, "src"), "PBXSourcesBuildPhase", app_sources),
              phase(uid("phase", APP, "fw"), "PBXFrameworksBuildPhase", []),
              phase(uid("phase", APP, "res"), "PBXResourcesBuildPhase", [])]
test_phases = [phase(uid("phase", TESTS, "src"), "PBXSourcesBuildPhase", test_sources),
               phase(uid("phase", TESTS, "fw"), "PBXFrameworksBuildPhase", []),
               phase(uid("phase", TESTS, "res"), "PBXResourcesBuildPhase", test_resources)]


def settings(d):
    out = []
    for k, v in d.items():
        if isinstance(v, list):
            out.append('%s = (%s);' % (k, " ".join('"%s",' % x for x in v)))
        else:
            out.append('%s = "%s";' % (k, v))
    return "{" + " ".join(out) + "}"


def config_list(name, build_settings):
    config = uid("config", name, "Debug")
    add(config, "{isa = XCBuildConfiguration; buildSettings = %s; name = Debug; }" % settings(build_settings))
    key = uid("configlist", name)
    add(key, f"{{isa = XCConfigurationList; buildConfigurations = ({config}, ); defaultConfigurationIsVisible = 0; defaultConfigurationName = Debug; }}")
    return key


project_settings = {
    "SDKROOT": "iphoneos",
    "SUPPORTED_PLATFORMS": "iphonesimulator iphoneos",
    "IPHONEOS_DEPLOYMENT_TARGET": "12.0",
    "CLANG_ENABLE_OBJC_ARC": "YES",
    "CLANG_ENABLE_MODULES": "YES",
    # Same language mode as Unity's generated Xcode project (strict C11: no `typeof`, no GNU keywords).
    "GCC_C_LANGUAGE_STANDARD": "c11",
    "ONLY_ACTIVE_ARCH": "YES",
    "DEBUG_INFORMATION_FORMAT": "dwarf",
    "GCC_OPTIMIZATION_LEVEL": "0",
    "ENABLE_TESTABILITY": "YES",
    "CODE_SIGN_IDENTITY": "-",
    "CODE_SIGN_STYLE": "Manual",
    "HEADER_SEARCH_PATHS": ["$(SRCROOT)/" + PLUGIN_REL, "$(SRCROOT)/HostApp"],
    "TARGETED_DEVICE_FAMILY": "1,2",
}
app_settings = {
    "PRODUCT_NAME": APP,
    "PRODUCT_BUNDLE_IDENTIFIER": "com.flyingacorn.soil.ads.harness",
    "GENERATE_INFOPLIST_FILE": "YES",
    "INFOPLIST_KEY_UILaunchScreen_Generation": "YES",
    "INFOPLIST_KEY_UISupportedInterfaceOrientations": "UIInterfaceOrientationPortrait UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight",
    "INFOPLIST_KEY_UISupportedInterfaceOrientations_iPad": "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight",
    # Like Unity's UnityFramework target: Objective-C exceptions are off, so @try/@throw only
    # compile in files whose .meta CompileFlags add -fobjc-exceptions (copied per file above).
    "GCC_ENABLE_OBJC_EXCEPTIONS": "NO",
    # The plugin must build warning-free.
    "WARNING_CFLAGS": ["-Wall", "-Wextra", "-Wpedantic", "-Wno-unused-parameter", "-Wmissing-prototypes", "-Wunguarded-availability"],
    "GCC_TREAT_WARNINGS_AS_ERRORS": "YES",
}
test_settings = {
    "PRODUCT_NAME": TESTS,
    "PRODUCT_BUNDLE_IDENTIFIER": "com.flyingacorn.soil.ads.harness.tests",
    "GENERATE_INFOPLIST_FILE": "YES",
    "TEST_HOST": f"$(BUILT_PRODUCTS_DIR)/{APP}.app/{APP}",
    "BUNDLE_LOADER": "$(TEST_HOST)",
    # The tests themselves throw on purpose (the guard's tests).
    "GCC_ENABLE_OBJC_EXCEPTIONS": "YES",
    "IPHONEOS_DEPLOYMENT_TARGET": "13.0",
    # broken.png is deliberately not a PNG.
    "COMPRESS_PNG_FILES": "NO",
    "STRIP_PNG_TEXT": "NO",
}

app_target = uid("target", APP)
test_target = uid("target", TESTS)
project = uid("project")
proxy = uid("proxy")
dependency = uid("dependency")
add(proxy, f"{{isa = PBXContainerItemProxy; containerPortal = {project}; proxyType = 1; remoteGlobalIDString = {app_target}; remoteInfo = {APP}; }}")
add(dependency, f"{{isa = PBXTargetDependency; target = {app_target}; targetProxy = {proxy}; }}")
add(app_target, "{isa = PBXNativeTarget; buildConfigurationList = %s; buildPhases = (%s); buildRules = (); dependencies = (); name = %s; productName = %s; productReference = %s; productType = \"com.apple.product-type.application\"; }"
    % (config_list(APP, app_settings), " ".join(p + "," for p in app_phases), APP, APP, app_product))
add(test_target, "{isa = PBXNativeTarget; buildConfigurationList = %s; buildPhases = (%s); buildRules = (); dependencies = (%s, ); name = %s; productName = %s; productReference = %s; productType = \"com.apple.product-type.bundle.unit-test\"; }"
    % (config_list(TESTS, test_settings), " ".join(p + "," for p in test_phases), dependency, TESTS, TESTS, test_product))
add(project, "{isa = PBXProject; attributes = {LastUpgradeCheck = 1600; TargetAttributes = {%s = {TestTargetID = %s; }; }; }; buildConfigurationList = %s; compatibilityVersion = \"Xcode 14.0\"; developmentRegion = en; hasScannedForEncodings = 0; knownRegions = (en, Base, ); mainGroup = %s; productRefGroup = %s; projectDirPath = \"\"; projectRoot = \"\"; targets = (%s, %s, ); }"
    % (test_target, app_target, config_list("project", project_settings), main_group, products_group, app_target, test_target))

os.makedirs(os.path.join(PROJECT, "xcshareddata", "xcschemes"), exist_ok=True)
with open(os.path.join(PROJECT, "project.pbxproj"), "w") as f:
    f.write("// !$*UTF8*$!\n{\n\tarchiveVersion = 1;\n\tclasses = {};\n\tobjectVersion = 56;\n\tobjects = {\n")
    for key in sorted(objects):
        f.write(f"\t\t{key} = {objects[key]};\n")
    f.write(f"\t}};\n\trootObject = {project};\n}}\n")


def ref(target, name, product):
    return (f'<BuildableReference BuildableIdentifier = "primary" BlueprintIdentifier = "{target}" '
            f'BuildableName = "{product}" BlueprintName = "{name}" ReferencedContainer = "container:SoilAdsHarness.xcodeproj"/>')


scheme = f"""<?xml version="1.0" encoding="UTF-8"?>
<Scheme LastUpgradeVersion = "1600" version = "1.7">
   <BuildAction parallelizeBuildables = "YES" buildImplicitDependencies = "YES">
      <BuildActionEntries>
         <BuildActionEntry buildForTesting = "YES" buildForRunning = "YES" buildForProfiling = "YES" buildForArchiving = "YES" buildForAnalyzing = "YES">
            {ref(app_target, APP, APP + ".app")}
         </BuildActionEntry>
      </BuildActionEntries>
   </BuildAction>
   <TestAction buildConfiguration = "Debug" selectedDebuggerIdentifier = "Xcode.DebuggerFoundation.Debugger.LLDB" selectedLauncherIdentifier = "Xcode.DebuggerFoundation.Launcher.LLDB" shouldUseLaunchSchemeArgsEnv = "YES">
      <Testables>
         <TestableReference skipped = "NO">
            {ref(test_target, TESTS, TESTS + ".xctest")}
         </TestableReference>
      </Testables>
   </TestAction>
   <LaunchAction buildConfiguration = "Debug" selectedDebuggerIdentifier = "Xcode.DebuggerFoundation.Debugger.LLDB" selectedLauncherIdentifier = "Xcode.DebuggerFoundation.Launcher.LLDB" launchStyle = "0" useCustomWorkingDirectory = "NO" ignoresPersistentStateOnLaunch = "NO" debugDocumentVersioning = "YES" debugServiceExtension = "internal" allowLocationSimulation = "YES">
      <BuildableProductRunnable runnableDebuggingMode = "0">
         {ref(app_target, APP, APP + ".app")}
      </BuildableProductRunnable>
   </LaunchAction>
</Scheme>
"""
with open(os.path.join(PROJECT, "xcshareddata", "xcschemes", "SoilAdsHarness.xcscheme"), "w") as f:
    f.write(scheme)
print("wrote", PROJECT)
