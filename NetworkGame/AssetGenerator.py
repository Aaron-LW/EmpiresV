import os 
import base64

dir_path = os.path.dirname(os.path.realpath(__file__))
asset_dir_path = dir_path + os.sep + "Assets"

print("Asset directory: " + asset_dir_path)

assets_list = os.listdir(asset_dir_path)
binary_list = []

out_file = open(dir_path + os.sep + "GeneratedAssets.cs", "w")
out_file.write(f"public static class GeneratedAssets\n")
out_file.write("{\n")
out_file.write("    public static string[] Assets = [\n")

for asset in assets_list:

    asset_name, extension = os.path.splitext(asset)
    if (extension == ".cs"):
        break

    print(f"Generating: {asset}")

    with open(asset_dir_path + os.sep + asset, "rb") as f:
        binary = f.read()

    encoded = base64.b64encode(binary).decode("ascii")
    out_file.write(f'           """{asset}|{encoded}""",\n')


out_file.write("        ];\n")
out_file.write("}")