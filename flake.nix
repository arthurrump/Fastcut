{
  description = "A tool for fast video editing using ffmpeg.";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs";
    flake-parts.url = "github:hercules-ci/flake-parts";
    nuget-packageslock2nix = {
      url = "github:mdarocha/nuget-packageslock2nix/main";
      inputs.nixpkgs.follows = "nixpkgs";
    };
  };

  outputs = inputs@{ flake-parts, self, nuget-packageslock2nix, ... }:
    flake-parts.lib.mkFlake { inherit inputs; } {
      systems = [ "x86_64-linux" "aarch64-linux" "aarch64-darwin" "x86_64-darwin" ];
      perSystem = { config, self', inputs', pkgs, system, ... }: 
        let
          runtimeDeps = with pkgs; [ ffmpeg ];
          version = "1.0.0-" + self.shortRev;
        in
        {
          packages.default = pkgs.buildDotnetModule {
            pname = "fastcut";
            inherit version;

            src = ./.;
            projectFile = "Fastcut.fsproj";

            dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
            dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;

            nugetDeps = nuget-packageslock2nix.lib {
              inherit system;
              lockfiles = [
                ./packages.lock.json
              ];
            };

            executables = [ "fastcut" ];

            inherit runtimeDeps;
          };
        };
    };
}
