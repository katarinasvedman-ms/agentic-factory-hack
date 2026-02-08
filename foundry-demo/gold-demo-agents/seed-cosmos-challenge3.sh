#!/bin/bash
# =============================================================================
# GOLD Demo - Seed Additional Containers for Challenge 3 Agents
# =============================================================================
# Adds containers needed for MaintenanceScheduler and PartsOrder agents:
# - WorkOrders
# - MaintenanceWindows
# - MaintenanceSchedules
# - Suppliers
# - PartsOrders
#
# Usage:
#   ./seed-cosmos-challenge3.sh
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

COSMOS_ENDPOINT="${GOLD_COSMOS_ENDPOINT:-https://gold-demo-cosmos.documents.azure.com:443/}"

echo "🚀 Seeding Challenge 3 containers for MaintenanceScheduler and PartsOrder"
echo "   Endpoint: $COSMOS_ENDPOINT"
echo ""

# Install dependencies
pip3 install azure-cosmos azure-identity --quiet 2>/dev/null || true

python3 << 'EOF'
import json
from azure.cosmos import CosmosClient
from azure.cosmos.partition_key import PartitionKey
from azure.identity import DefaultAzureCredential

ENDPOINT = "https://gold-demo-cosmos.documents.azure.com:443/"
DATABASE = "FactoryOpsDB"

# Additional data for Challenge 3 agents
CHALLENGE3_DATA = {
    "WorkOrders": [
        {
            "id": "WO-20260207-0001",
            "workOrderNumber": "WO-20260207-0001",
            "machineId": "TBM-001",
            "title": "Bearing Replacement - High Vibration",
            "description": "Replace drum bearing due to excessive vibration detected",
            "faultType": "building_drum_vibration",
            "priority": "high",
            "status": "pending",
            "assignedTo": "TECH-001",
            "createdDate": "2026-02-07T10:00:00Z",
            "estimatedDuration": 120,
            "partsRequired": [{"partNumber": "TBM-BRG-6220", "quantity": 1}]
        },
        {
            "id": "WO-20260207-0002",
            "workOrderNumber": "WO-20260207-0002",
            "machineId": "TCP-001",
            "title": "Heater Replacement - Temperature Excessive",
            "description": "Replace heating element due to temperature control issues",
            "faultType": "curing_temperature_excessive",
            "priority": "medium",
            "status": "pending",
            "assignedTo": "TECH-002",
            "createdDate": "2026-02-07T11:00:00Z",
            "estimatedDuration": 180,
            "partsRequired": [{"partNumber": "TCP-HTR-4KW", "quantity": 1}, {"partNumber": "GEN-TS-K400", "quantity": 2}]
        }
    ],
    "MaintenanceWindows": [
        {
            "id": "MW-20260208-NIGHT",
            "startTime": "2026-02-08T22:00:00Z",
            "endTime": "2026-02-09T06:00:00Z",
            "productionImpact": "Low",
            "isAvailable": True,
            "shift": "Night",
            "description": "Weekend night shift - minimal production"
        },
        {
            "id": "MW-20260209-DAY",
            "startTime": "2026-02-09T08:00:00Z",
            "endTime": "2026-02-09T16:00:00Z",
            "productionImpact": "Medium",
            "isAvailable": True,
            "shift": "Day",
            "description": "Sunday day shift - reduced production"
        },
        {
            "id": "MW-20260210-NIGHT",
            "startTime": "2026-02-10T22:00:00Z",
            "endTime": "2026-02-11T06:00:00Z",
            "productionImpact": "Medium",
            "isAvailable": True,
            "shift": "Night",
            "description": "Weekday night shift"
        }
    ],
    "Suppliers": [
        {
            "id": "SUP-001",
            "name": "Nordic Industrial Parts AB",
            "category": "bearings",
            "leadTimeDays": 2,
            "reliabilityScore": 0.95,
            "location": "Stockholm",
            "partsSupplied": ["TBM-BRG-6220", "TBM-LS-500N"],
            "prices": {"TBM-BRG-6220": 450.00, "TBM-LS-500N": 280.00}
        },
        {
            "id": "SUP-002",
            "name": "Euro Heating Systems GmbH",
            "category": "heating",
            "leadTimeDays": 5,
            "reliabilityScore": 0.92,
            "location": "Munich",
            "partsSupplied": ["TCP-HTR-4KW"],
            "prices": {"TCP-HTR-4KW": 890.00}
        },
        {
            "id": "SUP-003",
            "name": "Sensor Tech International",
            "category": "sensors",
            "leadTimeDays": 3,
            "reliabilityScore": 0.98,
            "location": "Copenhagen",
            "partsSupplied": ["GEN-TS-K400", "TBM-LS-500N"],
            "prices": {"GEN-TS-K400": 75.00, "TBM-LS-500N": 295.00}
        },
        {
            "id": "SUP-004",
            "name": "Motor Solutions Europe",
            "category": "motors",
            "leadTimeDays": 7,
            "reliabilityScore": 0.90,
            "location": "Amsterdam",
            "partsSupplied": ["TBM-SRV-5KW"],
            "prices": {"TBM-SRV-5KW": 2200.00}
        }
    ],
    "MaintenanceSchedules": [],  # Empty - will be written by agent
    "PartsOrders": []  # Empty - will be written by agent
}

def main():
    print("📦 Connecting to Cosmos DB with Managed Identity...")
    
    credential = DefaultAzureCredential()
    client = CosmosClient(ENDPOINT, credential=credential)
    database = client.get_database_client(DATABASE)
    
    print(f"✅ Connected to database '{DATABASE}'")
    
    # Container configurations
    containers_config = {
        "WorkOrders": "/machineId",
        "MaintenanceWindows": "/shift",
        "MaintenanceSchedules": "/workOrderId",
        "Suppliers": "/category",
        "PartsOrders": "/workOrderId"
    }
    
    for container_name, pk_path in containers_config.items():
        try:
            container = database.create_container_if_not_exists(
                id=container_name,
                partition_key=PartitionKey(path=pk_path)
            )
            print(f"✅ Container '{container_name}' ready (pk: {pk_path})")
        except Exception as e:
            container = database.get_container_client(container_name)
            print(f"✅ Container '{container_name}' exists")
        
        # Seed data if available
        data = CHALLENGE3_DATA.get(container_name, [])
        for item in data:
            try:
                container.upsert_item(item)
            except Exception as e:
                print(f"   Warning: Could not upsert {item.get('id')}: {e}")
        
        if data:
            print(f"   → {len(data)} items seeded")
    
    print("")
    print("🎉 Challenge 3 containers ready!")
    print("")
    print("Now create the MCP tool with write actions in Logic App")

if __name__ == "__main__":
    main()
EOF

echo ""
echo "✅ Challenge 3 seed complete."
