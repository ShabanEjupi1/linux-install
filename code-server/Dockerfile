FROM codercom/code-server:latest

# Rebrand the bundled VS Code: window title and about dialog show SpaceCode.
# applicationName/dataFolderName are left untouched so CLI paths and the
# extensions directory keep working across upgrades.
USER root
RUN sed -i \
    -e 's/"nameShort": "code-server"/"nameShort": "SpaceCode"/' \
    -e 's/"nameLong": "code-server"/"nameLong": "SpaceCode"/' \
    /usr/lib/code-server/lib/vscode/product.json
USER coder
