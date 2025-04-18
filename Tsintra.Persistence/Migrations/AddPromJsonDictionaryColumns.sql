-- Add JSON columns for Dictionary<string, string> properties in PromProducts and PromGroups tables

-- First check if PromProducts table exists before modifying
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'promproducts') THEN
        -- Add JSON columns to PromProducts table if they don't exist
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promproducts' AND column_name = 'namemultilangjson') THEN
            ALTER TABLE PromProducts ADD COLUMN NameMultilangJson JSONB;
        END IF;
        
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promproducts' AND column_name = 'descriptionmultilangjson') THEN
            ALTER TABLE PromProducts ADD COLUMN DescriptionMultilangJson JSONB;
        END IF;
        
        -- Copy data from existing multilang tables if they exist
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'promproductmultilang') THEN
            -- Create temporary function to aggregate multilang data
            CREATE OR REPLACE FUNCTION temp_aggregate_product_multilang(product_id bigint)
            RETURNS TABLE(name_json jsonb, description_json jsonb) AS $$
            DECLARE
                name_data jsonb := '{}'::jsonb;
                desc_data jsonb := '{}'::jsonb;
                rec RECORD;
            BEGIN
                FOR rec IN (SELECT languagecode, name, description FROM PromProductMultilang WHERE ProductId = product_id) LOOP
                    IF rec.name IS NOT NULL THEN
                        name_data := name_data || jsonb_build_object(rec.languagecode, rec.name);
                    END IF;
                    
                    IF rec.description IS NOT NULL THEN
                        desc_data := desc_data || jsonb_build_object(rec.languagecode, rec.description);
                    END IF;
                END LOOP;
                
                RETURN QUERY SELECT name_data, desc_data;
            END;
            $$ LANGUAGE plpgsql;
            
            -- Update product records with JSON data
            UPDATE PromProducts p
            SET NameMultilangJson = agg.name_json,
                DescriptionMultilangJson = agg.description_json
            FROM (
                SELECT product_id, name_json, description_json
                FROM (SELECT DISTINCT ProductId as product_id FROM PromProductMultilang) AS distinct_products
                CROSS JOIN LATERAL temp_aggregate_product_multilang(distinct_products.product_id) AS agg
            ) AS agg
            WHERE p.Id = agg.product_id;
            
            -- Drop temporary function
            DROP FUNCTION IF EXISTS temp_aggregate_product_multilang(bigint);
        END IF;
    END IF;
    
    -- Check if PromGroups table exists before modifying
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'promgroups') THEN
        -- Add JSON columns to PromGroups table if they don't exist
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promgroups' AND column_name = 'namemultilangjson') THEN
            ALTER TABLE PromGroups ADD COLUMN NameMultilangJson JSONB;
        END IF;
        
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'promgroups' AND column_name = 'descriptionmultilangjson') THEN
            ALTER TABLE PromGroups ADD COLUMN DescriptionMultilangJson JSONB;
        END IF;
        
        -- Copy data from existing multilang tables if they exist
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'promgroupmultilang') THEN
            -- Create temporary function to aggregate multilang data
            CREATE OR REPLACE FUNCTION temp_aggregate_group_multilang(group_id bigint)
            RETURNS TABLE(name_json jsonb, description_json jsonb) AS $$
            DECLARE
                name_data jsonb := '{}'::jsonb;
                desc_data jsonb := '{}'::jsonb;
                rec RECORD;
            BEGIN
                FOR rec IN (SELECT languagecode, name, description FROM PromGroupMultilang WHERE GroupId = group_id) LOOP
                    IF rec.name IS NOT NULL THEN
                        name_data := name_data || jsonb_build_object(rec.languagecode, rec.name);
                    END IF;
                    
                    IF rec.description IS NOT NULL THEN
                        desc_data := desc_data || jsonb_build_object(rec.languagecode, rec.description);
                    END IF;
                END LOOP;
                
                RETURN QUERY SELECT name_data, desc_data;
            END;
            $$ LANGUAGE plpgsql;
            
            -- Update group records with JSON data
            UPDATE PromGroups g
            SET NameMultilangJson = agg.name_json,
                DescriptionMultilangJson = agg.description_json
            FROM (
                SELECT group_id, name_json, description_json
                FROM (SELECT DISTINCT GroupId as group_id FROM PromGroupMultilang) AS distinct_groups
                CROSS JOIN LATERAL temp_aggregate_group_multilang(distinct_groups.group_id) AS agg
            ) AS agg
            WHERE g.Id = agg.group_id;
            
            -- Drop temporary function
            DROP FUNCTION IF EXISTS temp_aggregate_group_multilang(bigint);
        END IF;
    END IF;
END $$; 