def wrapperConfigurations = [
  Debug: 'dbg',
  Release: ''
]
def configuration = 'Debug'

def nuget = '/usr/local/bin/nuget'
def mdtool = '/Applications/Xamarin Studio.app/Contents/MacOS/mdtool'
def xbuild = '/usr/local/bin/xbuild'
def mono = '/usr/local/bin/mono'

stage('Checkout') {
  node {
    checkout([
        $class: 'GitSCM',
        branches: scm.branches,
        gitTool: 'native git',
        extensions: scm.extensions + [
          [$class: 'CleanCheckout'],
          [$class: 'SubmoduleOption', recursiveSubmodules: true]
        ],
        userRemoteConfigs: scm.userRemoteConfigs
      ])
      stash includes: '**/*', name: 'dotnet-source'
  }
}

def getArchive() {
    sh 'rm -rf *'
    unstash 'dotnet-source'
}

stage('Build') {
  parallel(
    'iOS': {
      node('osx') {
        getArchive()

        dir('wrappers') {
          sh "make ios${wrapperConfigurations[configuration]}"
        }

        stash includes: "wrappers/build/${configuration}-ios-universal/*", name: 'ios-wrappers-sync'
      }
      node('xamarin-mac') {
        getArchive()

        unstash 'ios-wrappers-sync'

        sh "${nuget} restore Realm.sln"

        // mdtool occasionally hangs, so put a timeout on it
        timeout(time: 8, unit: 'MINUTES') {
          sh "\"${mdtool}\" build -c:${configuration}\\|iPhoneSimulator Realm.sln -p:Tests.XamarinIOS"
        }

        stash includes: "Platform.XamarinIOS/Realm.XamarinIOS/bin/iPhoneSimulator/${configuration}/Realm.dll", name: 'nuget-ios'

        dir("Platform.XamarinIOS/Tests.XamarinIOS/bin/iPhoneSimulator/${configuration}") {
          stash includes: 'Tests.XamarinIOS.app/**/*', name: 'ios-tests'
        }
      }
    },
    'Android': {
      node('xamarin-mac') {
        getArchive()
        def workspace = pwd()

        dir('wrappers') {
          withEnv(["NDK_ROOT=${env.HOME}/Library/Developer/Xamarin/android-ndk/android-ndk-r10e"]) {
            sh "make android${wrapperConfigurations[configuration]}"
          }
        }

        sh "${nuget} restore Realm.sln"

        dir('Platform.XamarinAndroid/Tests.XamarinAndroid') {
          // define the SolutionDir build setting because Fody depends on it to discover weavers
          sh "${xbuild} Tests.XamarinAndroid.csproj /p:Configuration=${configuration} /t:SignAndroidPackage /p:AndroidUseSharedRuntime=false /p:EmbedAssembliesIntoApk=True /p:SolutionDir=\"${workspace}/\""
          dir("bin/${configuration}") {
            stash includes: 'io.realm.xamarintests-Signed.apk', name: 'android-tests'
          }
        }
        stash includes: "Platform.XamarinAndroid/Realm.XamarinAndroid/bin/${configuration}/Realm.dll,wrappers/build/${configuration}-android/*/libwrappers.so", name: 'nuget-android'
      }
    },
    'PCL': {
      node('xamarin-mac') {
        getArchive()
        sh "${nuget} restore Realm.sln"
        sh "${xbuild} Platform.PCL/Realm.PCL/Realm.PCL.csproj /p:Configuration=${configuration}"
        stash includes: "Platform.PCL/Realm.PCL/bin/${configuration}/Realm.dll,Platform.PCL/Realm.PCL/bin/${configuration}/Realm.XML", name: 'nuget-pcl'
      }
    }
  )
}

stage('Test') {
  parallel(
    'iOS': {
      node('osx') {
        unstash 'ios-tests'

        dir('Tests.XamarinIOS.app') {
          sh 'mkdir -p fakehome/Documents'
          sh "HOME=`pwd`/fakehome DYLD_ROOT_PATH=`xcrun -show-sdk-path -sdk iphonesimulator` ./Tests.XamarinIOS --headless"
          publishTests 'fakehome/Documents/TestResults.iOS.xml'
        }
      }
    },
    'Android': {
      node('android-hub') {
        sh 'rm -rf *'
        unstash 'android-tests'
        sh 'adb devices'
        sh 'adb devices | grep -v List | grep -v ^$ | awk \'{print $1}\' | parallel \'adb -s {} uninstall io.realm.xamarintests; adb -s {} install io.realm.xamarintests-Signed.apk; adb -s {} shell am instrument -w -r io.realm.xamarintests/.TestRunner; adb -s {} shell run-as io.realm.xamarintests cat /data/data/io.realm.xamarintests/files/TestResults.Android.xml > TestResults.Android_{}.xml\''
        publishTests()
      }
    },
    'Weaver': {
      node('xamarin-mac') {
        getArchive()
        def workspace = pwd()
        sh "${nuget} restore Realm.sln"

        dir('Weaver/WeaverTests/RealmWeaver.Tests') {
          sh "${xbuild} RealmWeaver.Tests.csproj /p:Configuration=${configuration}"
          sh "${mono} \"${workspace}\"/packages/NUnit.ConsoleRunner.*/tools/nunit3-console.exe RealmWeaver.Tests.csproj --result=TestResult.xml\\;format=nunit2 --config=${configuration} --inprocess"
          publishTests 'TestResult.xml'
        }
        stash includes: "Weaver/RealmWeaver.Fody/bin/${configuration}/RealmWeaver.Fody.dll", name: 'nuget-weaver'
      }
    }
  )
}

stage('NuGet') {
  node('xamarin-mac') {
    getArchive()

    unstash 'nuget-weaver'
    unstash 'nuget-pcl'
    unstash 'nuget-ios'
    unstash 'nuget-android'

    def version = readAssemblyVersion()
    def versionString = "${version.major}.${version.minor}.${version.patch}"

    dir('NuGet/NuGet.Library') {
      sh "${nuget} pack Realm.nuspec -version ${versionString} -NoDefaultExcludes -Properties Configuration=${configuration}"
      archive "Realm.${versionString}.nupkg"
    }
  }
}

def readAssemblyVersion() {
  def assemblyInfo = readFile 'RealmAssemblyInfo.cs'

  def match = (assemblyInfo =~ /\[assembly: AssemblyVersion\("(\d*).(\d*).(\d*).0"\)\]/)
  if (match) {
    return [
      major: match[0][1],
      minor: match[0][2],
      patch: match[0][3]
    ]
  } else {
    throw new Exception('Could not match Realm assembly version')
  }
}

def publishTests(filePattern='TestResults.*.xml') {
step([$class: 'XUnitPublisher', testTimeMargin: '3000', thresholdMode: 1, thresholds: [[$class: 'FailedThreshold', failureNewThreshold: '', failureThreshold: '1', unstableNewThreshold: '', unstableThreshold: ''], [$class: 'SkippedThreshold', failureNewThreshold: '', failureThreshold: '', unstableNewThreshold: '', unstableThreshold: '']], tools: [[$class: 'NUnitJunitHudsonTestType', deleteOutputFiles: true, failIfNotNew: true, pattern: filePattern, skipNoTestFiles: false, stopProcessingIfError: true]]])
}