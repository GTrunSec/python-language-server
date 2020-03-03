with import <nixpkgs> {};
let
  rpath = stdenv.lib.makeLibraryPath [ stdenv.cc.cc libunwind libuuid icu openssl zlib curl ];
in
stdenv.mkDerivation {
  name = "env";
  buildInputs = [
     (dotnet-sdk.overrideDerivation (oldAttrs: {
      src = fetchurl {
        url = "https://download.microsoft.com/download/8/A/7/8A765126-50CA-4C6F-890B-19AE47961E4B/dotnet-sdk-2.1.402-linux-x64.tar.gz";
        sha256 = "00hrl8vcj6gqkyprkzssd72li7nl6hlm05zw23hddjnc6ldwhvh8";
      };
    }))
     sqlite
     openssl
  ];
  shellHook = ''
        export LD_LIBRARY_PATH="${rpath}:$LD_LIBRARY_PATH"
      '';
}
