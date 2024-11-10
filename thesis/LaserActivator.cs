using System;
using System.Collections.Generic;

public class LaserActivator
{
    private double laserPosition;
    public LaserActivator(double laserPosition)
    {
        this.laserPosition = laserPosition;
    }

    public void findActivate(List<BoundingBoxes> boundingBoxes)
    {
        foreach (BoundingBoxes boundingBox in boundingBoxes)
        {
            if (boundingBox.Y <= laserPosition && (boundingBox.Y + boundingBox.Height) >= laserPosition)
            {
                activateLaser(boundingBox);
            }
        }
    }

    private void activateLaser(BoundingBoxes box)
    {

    }
}

public class activateData
{
}
