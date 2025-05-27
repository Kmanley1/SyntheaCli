# install-markdown-extensions.ps1
# Installs Markdown Git workflow and Beyond Compare extensions for Visual Studio Code

$extensions = @(
    # Markdown authoring
    "yzhang.markdown-all-in-one"        # All in One shortcuts table of contents and list helpers
    "DavidAnson.vscode-markdownlint"    # Linter for consistent Markdown style
    #"shd101wyy.markdown-preview-enhanced" # Rich preview with diagrams math and export
    "bierner.markdown-mermaid"          # Adds Mermaid diagram support to the preview
    #"mushan.vscode-paste-image"         # Paste clipboard image and insert reference automatically
    #"MS-CEINTL.vscode-wordcount"        # Shows live word count in the status bar
    #"streetsidesoftware.code-spell-checker" # Spell checker for prose and identifiers

    # Git workflow
    "eamodio.gitlens"                   # Line blame commit graph and history insights
    #"GitHub.vscode-pull-request-github" # Review pull requests and issues inside the editor
    #"mhutchie.git-graph"                # Visual commit graph with drag and drop actions
    #"donjayamanne.githistory"           # Quick file or repository history viewer
    #"GitHub.remotehub"                  # Browse and edit any GitHub repository without cloning

    # Beyond Compare integration
    "ScooterSoftware.bcompare-vscode"   # Use Beyond Compare for diff and merge tasks
)

foreach ($ext in $extensions) {
    Write-Host "Installing $ext ..."
    code --install-extension $ext --force
}

Write-Host "Extension installation complete."
