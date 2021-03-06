﻿using System.Collections.Generic;
using UnityEngine;

//------------------------------------------------------\\
//  A gun that can be used by an entity with            \\
//  a WeaponHolder that holds a GunWeaponTemplate.      \\
//------------------------------------------------------\\
//      Written by Joe for proof of concept phase       \\
//      and modified/optimised for prototype phase      \\
//------------------------------------------------------\\

public class GunWeapon : Weapon
{
    //Properties
    public int m_totalAmmo { get; private set; }                //Total ammo avaialble for the gun
    public int m_loadedAmmo { get; private set; }               //Ammo loaded in the gun
    public bool m_reloading { get; private set; }               //True when the gun is in the process of reloading
    public GunWeaponTemplate m_gunTemplate { get; private set; }//The template that defines the gun's base properties
    public bool m_torchOn { get; private set; }                 //Whether a torch is turned on for this gun

    private float m_reloadTimer;                                //Amount of time (seconds) until reloading is done
    private Queue<GameObject> m_bulletHoles = new Queue<GameObject>();  //All bullet holes that have been spawned when a wall was shot.
                                                                        //  Using queue so holes can be destroyed in the order they were instantiated when MAX_BULLET_HOLES is reached

    private const int MAX_BULLET_HOLES = 100;   //The maximum number of bullet holes that can be in a scene at any given time,
                                                //  prevents game lag due to a huge amount of bullet holes

    //Constructor
    public GunWeapon(WeaponHolder weaponHolder, GunWeaponTemplate template) : base(weaponHolder, template)
    {
        m_loadedAmmo = template.GetMagazineSize();
        m_gunTemplate = template;
        m_totalAmmo = template.GetTotalStartAmmo();
    }

    public bool ToggleTorchOn()
    {
        m_torchOn = !m_torchOn;
        return m_torchOn;
    }

    public override void HeldUpdate()
    {
        base.HeldUpdate();

        //Reload the gun if out of ammo
        if (m_loadedAmmo <= 0 && !m_reloading)
        {
            StartReload();
        }

        //Reduce the reload timer until reloading is done
        if (m_reloading)
        {
            m_reloadTimer -= Time.deltaTime;
            if (m_reloadTimer <= 0)
            {
                ReloadDone();
            }
        }
    }

    public override bool ReadyToAttack()
    {
        //Can only shoot if ammo is loaded and not in the process of reloading
        if ( (base.ReadyToAttack()) && (m_loadedAmmo > 0) && (!m_reloading) )
        {
            return true;
        }
        return false;
    }

    //Attack is called when the player shoots
    public override void Attack(WeaponAimInfo weaponAimInfo, GameObject gunGameObject, GameObject prefabFireLight, Transform transformHead, bool buttonDown)
    {
        if (!m_gunTemplate.GetContinuousFire() && !buttonDown)
        {
            //If continuous fire is disabled and the mouse button was not pressed on this frame, do not fire
            return;
        }

        //Reset attack interval to prevent continuous firing
        m_attackIntervalTimer = m_template.GetAttackInterval();

        //Instantiate fire particles and light
        GameObject fireParticles = m_gunTemplate.GetFireParticles();
        Transform parentMuzzle = gunGameObject.transform.Find("AimPoint");
        if (fireParticles != null)
        {
            Object.Instantiate(fireParticles, parentMuzzle);
        }
        Object.Instantiate(prefabFireLight, parentMuzzle);

        if (weaponAimInfo.m_raycastHit)
        {
            //Gun hit an object
            Debug.Log("Gun fired, hit " + weaponAimInfo.m_hitInfo.transform.name);

            GameObject goHit = weaponAimInfo.m_hitInfo.collider.gameObject;
            //Apply random attack damage (between set min and max) to hit enemies
            if (goHit.CompareTag("Enemy"))
            {
                int damageAmount = Random.Range(m_template.GetMinAttackDamage(), m_template.GetMaxAttackDamage() + 1);
                weaponAimInfo.m_hitInfo.transform.GetComponent<Enemy>().Damage(damageAmount);
                UIManager.instance.ShowEnemyHitPopup(damageAmount, weaponAimInfo.m_hitInfo.point);
            }
            //Explode any hit objects with the ExplodeOnImpact component
            else if (goHit.CompareTag("ExplodeOnImpact"))
            {
                goHit.GetComponent<ExplodeOnImpact>().Explode();
            }
            else if (goHit.CompareTag("Wall") || (goHit.transform.parent != null && goHit.transform.parent.CompareTag("Wall")))
            {
                if(CanCreateBulletHole(weaponAimInfo))
                {
                    CreateBulletHole(weaponAimInfo);
                }
                AudioManager.instance.PlaySoundEffect3D(m_gunTemplate.m_objectHitSound, weaponAimInfo.m_hitInfo.point, m_gunTemplate.m_objectHitSoundVolume);
            }
        }
        else
        {
            Debug.Log("Gun fired, hit nothing");
        }

        //Reduce ammo, or throw an error if shooting with no ammo
        if (m_loadedAmmo >= 0)
        {
            m_loadedAmmo--;
        }
        else { Debug.LogError("Shooting gun with no ammo (" + m_template.GetWeaponName() + ")"); }

        //Trigger the shoot animation and sound
        gunGameObject.transform.Find("Gun").GetComponent<Animator>().SetTrigger("Shoot");
        AudioManager.instance.PlaySoundEffect2D(m_template.m_attackSound, m_template.m_attackSoundVolume, 0.95f, 1.05f);
    }

    //Alternate melee attack for guns
    public override void AlternateAttack(WeaponAimInfo weaponAimInfo, GameObject weaponGameObject, Transform transformHead)
    {
        //Alternate attack only allowed for guns with melee ability
        if (!m_gunTemplate.GetCanUseAsMelee())
        {
            return;
        }

        //Reset attack interval to prevent continuous melee attacks
        m_alternateAttackIntervalTimer = m_gunTemplate.GetMeleeInterval();

        if (weaponAimInfo.m_raycastHit)
        {
            //Gun hit an object
            Debug.Log("Attacking with gun as melee weapon, hit " + weaponAimInfo.m_hitInfo.transform.name);

            GameObject goHit = weaponAimInfo.m_hitInfo.collider.gameObject;
            //Apply random melee attack damage (between set min and max) to hit enemies
            if (goHit.CompareTag("Enemy"))
            {
                int damageAmount = Random.Range(m_gunTemplate.GetMinMeleeAttackDamage(), m_gunTemplate.GetMaxMeleeAttackDamage() + 1);
                weaponAimInfo.m_hitInfo.transform.GetComponent<Enemy>().Damage(damageAmount);
                UIManager.instance.ShowEnemyHitPopup(damageAmount, weaponAimInfo.m_hitInfo.point);
            }
            //Explode any hit objects with the ExplodeOnImpact component
            else if (goHit.CompareTag("ExplodeOnImpact"))
            {
                goHit.GetComponent<ExplodeOnImpact>().Explode();
            }
        }
        else
        {
            Debug.Log("Attacking with gun as melee weapon, hit nothing");
        }


        //Trigger the melee animation and sound
        weaponGameObject.transform.Find("Gun").GetComponent<Animator>().SetTrigger("MeleeAttack");
        AudioManager.instance.PlaySoundEffect2D(m_gunTemplate.m_meleeSound, m_gunTemplate.m_meleeSoundVolume, 0.95f, 1.05f);
    }

    public void StartReload()
    {
        //Start reloading the gun if ammo is available, not already reloading and loaded ammo is not full
        if(!m_reloading && m_totalAmmo > 0 && m_loadedAmmo < m_gunTemplate.GetMagazineSize())
        {
            Debug.Log(m_template.GetWeaponName() + ": Starting reload");
            m_reloadTimer = m_gunTemplate.GetReloadTime();
            m_reloading = true;
            AudioManager.instance.PlaySoundEffect2D(m_gunTemplate.m_reloadSound, m_gunTemplate.m_reloadSoundVolume);
        }
    }

    private void ReloadDone()
    {
        Debug.Log(m_template.GetWeaponName() + ": Done reloading");

        m_reloading = false;
        //Set loaded ammo to the gun's magazine size, or the remaining amount of ammo if there is not enough for a full reload
        int reloadAmount = ((GunWeaponTemplate)m_template).GetMagazineSize() - m_loadedAmmo;
        if(reloadAmount > m_totalAmmo)
        {
            reloadAmount = m_totalAmmo;
        }
        m_loadedAmmo += reloadAmount;
        //Reduce totalAmmo by the ammo amount that was loaded
        m_totalAmmo -= reloadAmount;
    }

    public void AddAmmo(int amount)
    {
        m_totalAmmo += amount;
    }

    private bool CanCreateBulletHole(WeaponAimInfo aimInfo)
    {
        //Ignore the player when shooting rays
        int layerMask = (~LayerMask.GetMask("Player"));

        //rayOffsets are used to check that the edges of the bullet hole graphic will not appear to be floating outside of an object's geometry
        const float bulletHoleRadius = 0.05f;
        Vector3[] rayOffsets = new Vector3[] { m_weaponHolder.transform.right * bulletHoleRadius, m_weaponHolder.transform.up * -bulletHoleRadius,
                                               m_weaponHolder.transform.right * -bulletHoleRadius, m_weaponHolder.transform.up * bulletHoleRadius };
        for (int i = 0; i < rayOffsets.Length; i++)
        {
            Physics.Raycast(aimInfo.m_originPoint + rayOffsets[i], aimInfo.m_rayDirection, out RaycastHit hit, aimInfo.m_maxDistance, layerMask);

            //Test how far apart the two hit points are in their normal directions, used to ensure their depths are the same to prevent
            //  two surfaces with the same normals but different depths from registering as the same face (e.g. sides of stairs)
            float depthDifference = Vector3.Distance(Vector3.Scale(hit.point, hit.normal), Vector3.Scale(aimInfo.m_hitInfo.point, aimInfo.m_hitInfo.normal));

            //If any offset raycasts hit a different object to the original ray, OR have a different normal, OR are outside of the depth threshold,
            //  the graphic will not display correctly so the bullet hole will not be created
            if ( (hit.transform.name != aimInfo.m_hitInfo.transform.name) || (hit.normal != aimInfo.m_hitInfo.normal) || (depthDifference > 0.05f) )
            {
                return false;
            }
        }
        //All tests passed, the bullet hole can be created
        return true;
    }

    private void CreateBulletHole(WeaponAimInfo aimInfo)
    {
        //Instantiate the bullet hole sprite and position it based on where the raycast hit
        GameObject goBulletHole = Object.Instantiate(GameUtilities.instance.prefabBulletHole);
        goBulletHole.transform.position = aimInfo.m_hitInfo.point + (aimInfo.m_hitInfo.normal * 0.001f);
        goBulletHole.transform.rotation = Quaternion.LookRotation(aimInfo.m_hitInfo.normal);

        //If the maximum allowed number of bullet holes was reached,
        //  destroy the one that was created the least recently and remove it from the queue
        if (m_bulletHoles.Count >= MAX_BULLET_HOLES)
        {
            Object.Destroy(m_bulletHoles.Dequeue());
        }
        //Add the newly created bullet hole to the queue
        m_bulletHoles.Enqueue(goBulletHole);
    }
}
