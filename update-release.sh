#!/bin/bash

# DotNet MCP Server Release Update Script
# This script builds and publishes a new release, then updates Claude Code configuration

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
PROJECT_DIR="/home/dan/code/dotnet-mcp"
SERVER_PROJECT="$PROJECT_DIR/src/DotNetMcp.Server"
DIST_DIR="$PROJECT_DIR/dist"
BINARY_NAME="DotNetMcp.Server"
MCP_SERVER_NAME="dotnet-mcp-vsa"

echo -e "${BLUE}🚀 Starting DotNet MCP Server Release Update${NC}"
echo "Project Directory: $PROJECT_DIR"
echo "Distribution Directory: $DIST_DIR"
echo ""

# Change to project directory
cd "$PROJECT_DIR"

# Step 1: Clean previous build
echo -e "${YELLOW}🧹 Cleaning previous build...${NC}"
if [ -d "$DIST_DIR" ]; then
    rm -rf "$DIST_DIR"
    echo "Removed existing dist directory"
fi

# Step 2: Build the project
echo -e "${YELLOW}🔨 Building project...${NC}"
if ! dotnet build src/DotNetMcp.Server; then
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Build successful${NC}"

# Step 3: Publish self-contained binary
echo -e "${YELLOW}📦 Publishing self-contained binary...${NC}"
if ! dotnet publish "$SERVER_PROJECT" \
    -c Release \
    --self-contained \
    -r linux-x64 \
    -o "$DIST_DIR" \
    --verbosity quiet; then
    echo -e "${RED}❌ Publish failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Published to $DIST_DIR${NC}"

# Step 4: Verify binary exists and is executable
BINARY_PATH="$DIST_DIR/$BINARY_NAME"
if [ ! -f "$BINARY_PATH" ]; then
    echo -e "${RED}❌ Binary not found at $BINARY_PATH${NC}"
    exit 1
fi

# Make sure it's executable
chmod +x "$BINARY_PATH"
echo -e "${GREEN}✅ Binary verified and made executable${NC}"

# Step 5: Test the binary
echo -e "${YELLOW}🧪 Testing binary...${NC}"
if ! echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | timeout 5s "$BINARY_PATH" >/dev/null 2>&1; then
    echo -e "${RED}❌ Binary test failed or timed out${NC}"
    exit 1
fi
echo -e "${GREEN}✅ Binary test passed${NC}"

# Step 6: Update Claude Code MCP configuration
echo -e "${YELLOW}🔄 Updating Claude Code MCP configuration...${NC}"

# Remove existing MCP server if it exists
if claude mcp list 2>/dev/null | grep -q "$MCP_SERVER_NAME"; then
    echo "Removing existing MCP server..."
    claude mcp remove "$MCP_SERVER_NAME"
fi

# Add the new MCP server
echo "Adding updated MCP server..."
if ! claude mcp add "$MCP_SERVER_NAME" "$BINARY_PATH"; then
    echo -e "${RED}❌ Failed to add MCP server to Claude Code${NC}"
    exit 1
fi

# Step 7: Verify MCP server is working
echo -e "${YELLOW}🔍 Verifying MCP server...${NC}"
if claude mcp list | grep -q "$MCP_SERVER_NAME.*Connected"; then
    echo -e "${GREEN}✅ MCP server connected successfully${NC}"
else
    echo -e "${RED}❌ MCP server connection failed${NC}"
    echo "Run 'claude mcp list' to check status"
    exit 1
fi

# Step 8: Test tool availability
echo -e "${YELLOW}🛠️  Testing tool availability...${NC}"
TOOL_COUNT=$(echo '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | "$BINARY_PATH" | jq -r '.result.tools | length' 2>/dev/null || echo "0")

if [ "$TOOL_COUNT" -eq 7 ]; then
    echo -e "${GREEN}✅ All 7 tools available${NC}"
else
    echo -e "${RED}❌ Expected 7 tools, found $TOOL_COUNT${NC}"
    exit 1
fi

# Summary
echo ""
echo -e "${GREEN}🎉 Release update completed successfully!${NC}"
echo ""
echo -e "${BLUE}📋 Summary:${NC}"
echo "  • Binary: $BINARY_PATH"
echo "  • MCP Server: $MCP_SERVER_NAME"
echo "  • Tools Available: $TOOL_COUNT"
echo ""
echo -e "${BLUE}🚀 Ready to use:${NC}"
echo "  • Start Claude Code in this directory: cd $PROJECT_DIR && claude"
echo "  • Or add MCP to other projects: claude mcp add $MCP_SERVER_NAME $BINARY_PATH"
echo ""
echo -e "${BLUE}🔧 Available Tools:${NC}"
echo "  • extract_method - Extract code into new methods"
echo "  • rename_symbol - Rename symbols across codebase" 
echo "  • extract_interface - Extract interfaces from classes"
echo "  • find_symbol - Find symbols with advanced filtering"
echo "  • get_class_context - Get comprehensive class analysis"
echo "  • analyze_project_structure - Analyze project architecture"
echo "  • find_symbol_usages - Find symbol usages with impact analysis"